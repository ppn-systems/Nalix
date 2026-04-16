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

    /// <summary>
    /// Gets or sets whether BufferLease pooling is enabled. Default is true.
    /// Primarily used for testing to avoid A-B-A reuse issues.
    /// </summary>
    internal static bool IsPoolingEnabled { get; set; } = true;

    // Tight lock-free free-list for BufferLease instance reuse.
    // ConcurrentStack.TryPop/Push = single CAS operation — ~2ns vs ObjectPoolManager's
    // ~150ns (3x Interlocked + ConcurrentDict + DateTime + string write per call).
    private static readonly System.Collections.Concurrent.ConcurrentStack<BufferLease> s_freeList = new();

    /// <summary>
    /// Pops a lease shell from the free-list (or creates a new one).
    /// Maintains the atomic free-list count for O(1) capacity checks.
    /// </summary>
    /*
     * [Lease Shell Pooling]
     * BufferLease itself is an object. To avoid GC pressure from millions 
     * of lease allocations, we pool the 'shells' (the BufferLease instances).
     * This makes BufferLease essentially allocation-free when rented.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static BufferLease RentLeaseShell()
    {
        if (IsPoolingEnabled && s_freeList.TryPop(out BufferLease? cached))
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
        }

        /// <summary>Rents a raw buffer (fallback / legacy callers).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] Rent(int capacity = 256) => Volatile.Read(ref s_rentFunc)(capacity);

        /// <summary>Returns a raw buffer to the pool (fallback path).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Return(byte[] array) => Volatile.Read(ref s_returnFunc)(array, true);

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

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Parameterless constructor for free-list reuse.
    /// Do not call directly — use <see cref="Rent"/>, <see cref="CopyFrom"/>, or <see cref="TakeOwnership"/>.
    /// </summary>
    public BufferLease() { }

    private void Initialize(byte[] buffer, int start, int length, bool zeroOnDispose)
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
        /*
         * [Reference Counting]
         * Increment the ref-count atomically. If multiple consumers need 
         * to hold the same buffer (e.g. broadcast), they call Retain().
         * The buffer is only returned to the pool when the last owner calls Dispose().
         */
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

            ByteArrayPool.Return(buf);
        }

        // Return the BufferLease shell to the free-list for reuse (single CAS, zero alloc).
        if (IsPoolingEnabled && Volatile.Read(ref s_freeListCount) < PoolMaxSize)
        {
            this.ResetForFreeList();
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
        /*
         * [Ownership Transfer]
         * This allows a consumer to 'take' the raw byte[] out of the lease.
         * The lease marks itself as 'detached' and will no longer return the 
         * buffer to the pool when disposed.
         */
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
        byte[] arr = ByteArrayPool.Rent(capacity);
        BufferLease lease = RentLeaseShell();
        lease.Initialize(arr, start: 0, length: 0, zeroOnDispose);
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
        byte[] arr = ByteArrayPool.Rent(src.Length);
        src.CopyTo(arr);
        BufferLease lease = RentLeaseShell();
        lease.Initialize(arr, start: 0, length: src.Length, zeroOnDispose);
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
        lease.Initialize(buffer, start: 0, length: length, zeroOnDispose);
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
        lease.Initialize(buffer, start, length, zeroOnDispose);
        return lease;
    }

    #endregion APIs
}
