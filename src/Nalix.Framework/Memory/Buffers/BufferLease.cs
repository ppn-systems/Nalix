// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using Nalix.Common.Abstractions;
using Nalix.Common.Exceptions;
using Nalix.Framework.Injection;

namespace Nalix.Framework.Memory.Buffers;

/// <summary>
/// Represents an owned lease of a pooled byte[] rented from <see cref="BufferPoolManager"/>.
/// Supports slice ownership (start + length) to enable zero-copy handoffs.
/// </summary>
[DebuggerNonUserCode]
[DebuggerDisplay("BufferLease Start={_start}, Len={Length}, Cap={Capacity}, Detached={_detached != 0}")]
public sealed class BufferLease : IBufferLease
{
    #region Static

    private const int PoolMaxSize = 8192;

    // Atomic counter for the free-list depth. ConcurrentStack.Count is O(n) — it
    // traverses the entire linked list on every call. At millions of Dispose() calls
    // per second this is a major hidden cost. The counter trades perfect accuracy
    // for O(1) per-call overhead; off-by-one races are harmless (we just keep or
    // drop one extra shell).
    private static int s_freeListCount;

    // Tight lock-free free-list for BufferLease instance reuse.
    // ConcurrentStack.TryPop/Push = single CAS operation — ~2ns vs ObjectPoolManager's
    // ~150ns (3x Interlocked + ConcurrentDict + DateTime + string write per call).
    private static readonly System.Collections.Concurrent.ConcurrentStack<BufferLease> s_freeList = new();

    /// <summary>
    /// Pops a lease shell from the free-list (or creates a new one).
    /// Maintains the atomic free-list count for O(1) capacity checks.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static BufferLease RentLeaseShell()
    {
        if (s_freeList.TryPop(out BufferLease? cached))
        {
            _ = Interlocked.Decrement(ref s_freeListCount);
            return cached;
        }

        return new BufferLease();
    }

    /// <summary>
    /// Provides a centralized buffer pooling abstraction.
    /// Falls back to <see cref="System.Buffers.ArrayPool{T}.Shared"/> if no <see cref="BufferPoolManager"/> is registered.
    /// </summary>
    /// <remarks>
    /// This class is optimized for high-performance scenarios by resolving the underlying pool implementation once
    /// during static initialization, avoiding runtime branching on each call.
    /// </remarks>
    public static class ByteArrayPool
    {
        private static Func<int, byte[]> s_rentFunc = System.Buffers.ArrayPool<byte>.Shared.Rent;
        private static Action<byte[], bool> s_returnFunc = System.Buffers.ArrayPool<byte>.Shared.Return;

        // Segment-aware rent: returns the exact ArraySegment(slab, offset, count) from the
        // managed pool. Used by Rent() / CopyFrom() to capture correct offset+count without
        // any external metadata tracking. Falls back to null (byte[] path) when no managed pool.
        private static Func<int, ArraySegment<byte>>? s_rentSegmentFunc;

        // Segment-aware return: routes exact ArraySegment back to the correct ring.
        private static Action<ArraySegment<byte>>? s_returnSegmentFunc;

        static ByteArrayPool()
        {
            BufferPoolManager? pool = InstanceManager.Instance.GetExistingInstance<BufferPoolManager>();

            if (pool != null)
            {
                Configure(pool);
            }
        }

        /// <summary>
        /// Configures the pooled backend. Call during startup to bind the managed slab pool.
        /// </summary>
        public static void Configure(BufferPoolManager manager)
        {
            ArgumentNullException.ThrowIfNull(manager);

            Volatile.Write(ref s_rentFunc, manager.Rent);
            Volatile.Write(ref s_returnFunc, manager.Return);
            Volatile.Write(ref s_rentSegmentFunc, manager.RentSegment);
            Volatile.Write(ref s_returnSegmentFunc, seg => manager.Return(seg));
        }

        /// <summary>Rents a raw buffer (fallback / legacy callers).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] Rent(int capacity = 256) => Volatile.Read(ref s_rentFunc)(capacity);

        /// <summary>Returns a raw buffer to the pool (fallback path).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Return(byte[] array) => Volatile.Read(ref s_returnFunc)(array, true);

        /// <summary>
        /// Rents an ArraySegment carrying the exact slab offset+count.
        /// Used by <see cref="BufferLease.Rent"/> and <see cref="BufferLease.CopyFrom"/>
        /// so <see cref="BufferLease.Dispose"/> can return the correct segment.
        /// Falls back to a full-array segment from System.Buffers.ArrayPool when no managed pool is active.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ArraySegment<byte> RentSegment(int capacity)
        {
            Func<int, ArraySegment<byte>>? segRent = Volatile.Read(ref s_rentSegmentFunc);
            if (segRent is not null)
            {
                return segRent(capacity);
            }

            // Fallback: no managed pool — use shared ArrayPool, full array span.
            byte[] arr = System.Buffers.ArrayPool<byte>.Shared.Rent(capacity);
            return new ArraySegment<byte>(arr, 0, arr.Length);
        }

        /// <summary>Returns a slab-backed segment to the pool.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void ReturnSegment(ArraySegment<byte> segment)
        {
            Action<ArraySegment<byte>>? segReturn = Volatile.Read(ref s_returnSegmentFunc);
            if (segReturn is not null)
            {
                segReturn(segment);
            }
            else if (segment.Array is not null)
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(segment.Array);
            }
        }
    }

    /// <summary>
    /// Maximum buffer size for stack allocation in <see cref="CopyFrom"/>. Larger buffers will be heap-allocated.
    /// </summary>
    public static readonly int StackAllocThreshold = 512; // 1KB threshold for stackalloc in CopyFrom

    #endregion Static

    #region Fields

#if DEBUG
    private const byte PoisonByte = 0xCD;
    private const bool EnablePoisonOnDispose = true;
#endif

    /// <summary>slice start (≥ 0, ≤ RawCapacity)</summary>
    private int _start;

    /// <summary>reference count (≥ 0)</summary>
    private int _refCount;

    /// <summary>0 = not detached, 1 = detached</summary>
    private int _detached;

    private byte[]? _buffer;

    // Slab segment metadata — populated when rented from the managed pool.
    // Enables Dispose() to return the exact ArraySegment without metadata lookup.
    private int _poolSegmentOffset;
    private int _poolSegmentCount;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Parameterless constructor for free-list reuse.
    /// Do not call directly — use <see cref="Rent"/>, <see cref="CopyFrom"/>, or <see cref="TakeOwnership"/>.
    /// </summary>
    public BufferLease() { }

    private void Initialize(byte[] buffer, int start, int length, bool zeroOnDispose, int poolSegmentOffset = 0, int poolSegmentCount = 0)
    {
        if ((uint)start > (uint)buffer.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(start));
        }

        if ((uint)length > (uint)(buffer.Length - start))
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        _refCount = 1;
        _detached = 0;
        _start = start;
        _buffer = buffer;
        _poolSegmentOffset = poolSegmentOffset;
        _poolSegmentCount = poolSegmentCount;

        this.Length = length;
        this.ZeroOnDispose = zeroOnDispose;
    }

    /// <summary>Resets all fields before returning to the free-list.</summary>
    private void ResetForFreeList()
    {
        _buffer = null;
        _start = 0;
        _refCount = 0;
        _detached = 0;
        _poolSegmentOffset = 0;
        _poolSegmentCount = 0;
        this.Length = 0;
        this.ZeroOnDispose = false;
        this.IsReliable = false;
    }

    /// <summary>
    /// Satisfies <see cref="IPoolable"/> contract. Internal pooling uses the free-list path;
    /// external callers should not need to call this directly.
    /// </summary>
    public void ResetForPool() => this.ResetForFreeList();

#if DEBUG
    /// <summary>
    /// Finalizer for debugging aid — detects if a lease is discarded without being released.
    /// Actual pool-wide leak detection is handled by BufferPoolManager.
    /// </summary>
    ~BufferLease()
    {
        if (_buffer != null && Volatile.Read(ref _detached) == 0)
        {
            // Optional: Log lease-level discard, but core leak detection is now in the pool.
            // _logger?.Warn($"BufferLease of size {_buffer.Length} discarded without Dispose.");
        }
    }
#endif

    #endregion Constructors

    #region Properties

    /// <summary>
    /// Gets or sets the valid payload length within the owned slice.
    /// </summary>
    public int Length { get; private set; }

    /// <inheritdoc/>
    public bool IsReliable { get; set; }

    /// <summary>
    /// Gets the capacity of the owned slice (from <c>_start</c> to end of the array).
    /// </summary>
    public int Capacity => _buffer is null ? 0 : _buffer.Length - _start;

    /// <summary>
    /// Gets the total capacity (underlying array length).
    /// </summary>
    public int RawCapacity => _buffer?.Length ?? 0;

    /// <summary>
    /// Gets or sets whether the slice should be zeroed before returning to the pool.
    /// </summary>
    public bool ZeroOnDispose { get; set; }

    /// <summary>
    /// Writable span over the valid payload slice.
    /// </summary>
    [SuppressMessage("Style", "IDE0301:Simplify collection initialization", Justification = "<Pending>")]
    public Span<byte> Span => _buffer is null ? Span<byte>.Empty : new Span<byte>(_buffer, _start, this.Length);

    /// <summary>
    /// Writable span over the full owned slice (capacity).
    /// </summary>
    [SuppressMessage("Style", "IDE0301:Simplify collection initialization", Justification = "<Pending>")]
    public Span<byte> SpanFull => _buffer is null ? Span<byte>.Empty : new Span<byte>(_buffer, _start, this.Capacity);

    /// <summary>
    /// Read-only view of the valid payload slice.
    /// </summary>
    public ReadOnlyMemory<byte> Memory => _buffer is null ? ReadOnlyMemory<byte>.Empty : new ReadOnlyMemory<byte>(_buffer, _start, this.Length);

    #endregion Properties

    #region APIs

#if DEBUG

    /// <summary>
    /// Convenient ArraySegment over the valid payload slice.
    /// </summary>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ArraySegment<byte> AsSegment()
        => _buffer is null ? default : new ArraySegment<byte>(_buffer, _start, this.Length);

#endif

    /// <summary>
    /// Increases the reference count so multiple consumers can hold this lease safely.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the lease has already released its underlying buffer.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the internal reference count becomes invalid.</exception>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Retain()
    {
        ObjectDisposedException.ThrowIf(_buffer is null, nameof(BufferLease));

        int newValue = Interlocked.Increment(ref _refCount);

        if (newValue <= 1)
        {
            // newValue == 1: ok (single owner) — overflow -> negative or 0
            throw new InternalErrorException(
                $"[{nameof(BufferLease)}] Invalid ref-count after Retain: value={newValue}, thread={System.Environment.CurrentManagedThreadId}.");
        }
    }

    /// <summary>
    /// Sets the valid payload length (must be 0..Capacity).
    /// </summary>
    /// <param name="length">The new payload length.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="length"/> is negative or larger than <see cref="Capacity"/>.</exception>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CommitLength(int length)
    {
        if ((uint)length > (uint)this.Capacity)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        this.Length = length;
    }

    /// <summary>
    /// Releases a reference. When the count reaches zero and not detached, returns the array to <see cref="BufferPoolManager"/>.
    /// </summary>
    /// <remarks>
    /// Double-dispose is tolerated and becomes a debug-only diagnostic instead of throwing.
    /// </remarks>
    [StackTraceHidden]
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        int newValue = Interlocked.Decrement(ref _refCount);

        if (newValue > 0)
        {
            return;
        }

        if (newValue < 0)
        {
#if DEBUG
            Debug.Fail($"BufferLease double dispose detected. RefCount went to {newValue}");
#endif
            return;
        }

        GC.SuppressFinalize(this);

        if (Volatile.Read(ref _detached) == 1)
        {
            _buffer = null;
            _start = 0;
            this.Length = 0;
            return;
        }

        byte[]? buf = Interlocked.Exchange(ref _buffer, null);
        int start = _start;
        int len = this.Length;
        int segOffset = _poolSegmentOffset;
        int segCount = _poolSegmentCount;

        _start = 0;
        this.Length = 0;
        _poolSegmentOffset = 0;
        _poolSegmentCount = 0;

        if (buf is not null)
        {
            if (len > 0)
            {
                Span<byte> slice = new(buf, start, len);

                if (this.ZeroOnDispose)
                {
                    slice.Clear();
                }
#if DEBUG
                if (EnablePoisonOnDispose)
                {
                    slice.Fill(PoisonByte);
                }
#endif
            }

            // Use the slab-aware segment path when available (segCount > 0).
            // This returns the exact ArraySegment(array, slabOffset, slabCount)
            // so the pool matches on Count instead of array.Length, enabling
            // correct batch-slab return without any external metadata tracking.
            if (segCount > 0)
            {
                ByteArrayPool.ReturnSegment(new ArraySegment<byte>(buf, segOffset, segCount));
            }
            else
            {
                ByteArrayPool.Return(buf);
            }
        }

        // Return the BufferLease shell to the free-list for reuse (single CAS, zero alloc).
        this.ResetForFreeList();
        if (Volatile.Read(ref s_freeListCount) < PoolMaxSize)
        {
            s_freeList.Push(this);
            _ = Interlocked.Increment(ref s_freeListCount);
        }
    }

    /// <summary>
    /// Transfers ownership of the underlying array slice to the caller (no pool return on Dispose).
    /// After a successful release, this instance becomes empty and disposing it is a no-op.
    /// Only allowed when this is the last reference (refCount == 1).
    /// </summary>
    /// <param name="buffer">The detached buffer.</param>
    /// <param name="start">The starting offset of the detached slice.</param>
    /// <param name="length">The length of the detached slice.</param>
    [StackTraceHidden]
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool ReleaseOwnership(out byte[]? buffer, out int start, out int length)
    {
        // Ensure single-owner detach (avoid breaking other holders)
        if (Volatile.Read(ref _refCount) != 1)
        {
            buffer = null; start = 0; length = 0;
            return false;
        }

        if (Interlocked.Exchange(ref _detached, 1) == 1)
        {
            buffer = null; start = 0; length = 0;
            return false; // Already detached
        }

        buffer = Interlocked.Exchange(ref _buffer, null);
        start = _start;
        length = this.Length;

        _start = 0;
        this.Length = 0;
        return buffer is not null;
    }

    /// <summary>
    /// Auto-rents a buffer from <see cref="BufferPoolManager"/> and returns a new empty slice [start=0, length=0].
    /// Caller writes to <see cref="SpanFull"/> then calls <see cref="CommitLength(int)"/>.
    /// </summary>
    /// <param name="capacity">The minimum capacity to rent.</param>
    /// <param name="zeroOnDispose">Whether to clear the buffer before returning it to the pool.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="capacity"/> is negative.</exception>
    /// <exception cref="OutOfMemoryException">Thrown when no backing array can be rented.</exception>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BufferLease Rent(
        int capacity,
        bool zeroOnDispose = false)
    {
        ArraySegment<byte> seg = ByteArrayPool.RentSegment(capacity);
        BufferLease lease = RentLeaseShell();
        // Use the slab segment offset as the slice start so Span/Memory views
        // point to the correct region of the (potentially shared) backing array.
        // poolSegmentOffset + poolSegmentCount enable correct slab-aware return
        // in Dispose() via ByteArrayPool.ReturnSegment().
        lease.Initialize(seg.Array!, start: seg.Offset, length: 0, zeroOnDispose, seg.Offset, seg.Count);
        return lease;
    }

    /// <summary>
    /// Creates a <see cref="BufferLease"/> by copying the source into a newly rented buffer.
    /// </summary>
    /// <param name="src">The source data to copy.</param>
    /// <param name="zeroOnDispose">Whether to clear the buffer before returning it to the pool.</param>
    /// <exception cref="OutOfMemoryException">Thrown when no backing array can be rented for the copied data.</exception>
    public static BufferLease CopyFrom(ReadOnlySpan<byte> src, bool zeroOnDispose = false)
    {
        ArraySegment<byte> seg = ByteArrayPool.RentSegment(src.Length);
        // Copy into the correct offset of the (potentially shared) slab array.
        src.CopyTo(new Span<byte>(seg.Array!, seg.Offset, seg.Count));
        BufferLease lease = RentLeaseShell();
        lease.Initialize(seg.Array!, start: seg.Offset, length: src.Length, zeroOnDispose, seg.Offset, seg.Count);
        return lease;
    }

    /// <summary>
    /// Wraps an array that was previously rented from <see cref="BufferPoolManager"/> (payload starts at 0).
    /// Caller asserts the array comes from the same pool and is safe to own here.
    /// </summary>
    /// <param name="buffer">The rented buffer to wrap.</param>
    /// <param name="length">The payload length within the buffer.</param>
    /// <param name="zeroOnDispose">Whether to clear the buffer before returning it to the pool.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="buffer"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="length"/> is outside the bounds of <paramref name="buffer"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BufferLease FromRented(
        byte[] buffer,
        int length,
        bool zeroOnDispose = false)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        BufferLease lease = RentLeaseShell();
        lease.Initialize(buffer, start: 0, length: length, zeroOnDispose, 0, 0);
        return lease;
    }

    /// <summary>
    /// Wraps a slice [<paramref name="start"/>..&lt;start+length&gt;) of a previously rented array from <see cref="BufferPoolManager"/>.
    /// This is the key API for zero-copy handoff of a payload located after a protocol header.
    /// </summary>
    /// <param name="buffer">The rented buffer to wrap.</param>
    /// <param name="start">The start offset of the slice.</param>
    /// <param name="length">The length of the slice.</param>
    /// <param name="zeroOnDispose">Whether to clear the buffer before returning it to the pool.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="buffer"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the requested slice is outside the bounds of <paramref name="buffer"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BufferLease TakeOwnership(
        byte[] buffer,
        int start,
        int length,
        bool zeroOnDispose = false)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        BufferLease lease = RentLeaseShell();
        lease.Initialize(buffer, start, length, zeroOnDispose, 0, 0);
        return lease;
    }

    #endregion APIs
}
