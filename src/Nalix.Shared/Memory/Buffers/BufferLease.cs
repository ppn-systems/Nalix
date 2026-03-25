// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using Nalix.Common.Shared;
using Nalix.Framework.Injection;

namespace Nalix.Shared.Memory.Buffers;

/// <summary>
/// Represents an owned lease of a pooled byte[] rented from <see cref="BufferPoolManager"/>.
/// Supports slice ownership (start + length) to enable zero-copy handoffs.
/// </summary>
[DebuggerNonUserCode]
[DebuggerDisplay("BufferLease Start={_start}, Len={Length}, Cap={Capacity}, Detached={_detached != 0}")]
public sealed class BufferLease : IBufferLease
{
    // ====== Static ======

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
        private static readonly Func<int, byte[]> RentFunc;
        private static readonly Action<byte[], bool> ReturnFunc;

        static ByteArrayPool()
        {
            BufferPoolManager? pool = InstanceManager.Instance.GetExistingInstance<BufferPoolManager>();

            if (pool != null)
            {
                RentFunc = pool.Rent;
                ReturnFunc = pool.Return;
            }
            else
            {
                System.Buffers.ArrayPool<byte> shared = System.Buffers.ArrayPool<byte>.Shared;
                RentFunc = shared.Rent;
                ReturnFunc = shared.Return;
            }
        }

        /// <summary>
        /// Rents a buffer with at least the specified capacity from the underlying pool.
        /// </summary>
        /// <param name="capacity">
        /// The minimum required length of the returned buffer.
        /// </param>
        /// <returns>
        /// A byte array that is at least <paramref name="capacity"/> in length.
        /// </returns>
        /// <remarks>
        /// The returned buffer may be larger than requested. The content of the buffer is undefined.
        /// </remarks>
        [MethodImpl(
            MethodImplOptions.AggressiveInlining)]
        public static byte[] Rent(int capacity = 256) => RentFunc(capacity);

        /// <summary>
        /// Returns a previously rented buffer to the pool.
        /// </summary>
        /// <param name="array">
        /// The buffer to return. Must not be <see langword="null"/>.
        /// </param>
        /// <remarks>
        /// The buffer must have been obtained via <see cref="Rent(int)"/>.
        /// After calling this method, the buffer should not be used again.
        /// </remarks>
        [MethodImpl(
            MethodImplOptions.AggressiveInlining)]
        public static void Return(byte[] array) => ReturnFunc(array, false);
    }

    /// <summary>
    /// Maximum buffer size for stack allocation in <see cref="CopyFrom"/>. Larger buffers will be heap-allocated.
    /// </summary>
    public static readonly int StackAllocThreshold = 512; // 1KB threshold for stackalloc in CopyFrom

#if DEBUG
    private const bool EnablePoisonOnDispose = true;
#else
    private const System.Boolean EnablePoisonOnDispose = false;
#endif

    private const byte PoisonByte = 0xCD;

    // ====== Fields ======

    /// <summary>
    /// slice start (&gt;= 0, &lt;= RawCapacity)
    /// </summary>
    private int _start;

    /// <summary>
    /// reference count (&gt;= 0)
    /// </summary>
    private int _refCount;

    /// <summary>
    /// 0 = no, 1 = yes
    /// </summary>
    private int _detached;

    private byte[]? _buffer;

    // ====== Ctor ======

    private BufferLease(byte[] buffer, int start, int length, bool zeroOnDispose)
    {
        ArgumentNullException.ThrowIfNull(buffer);

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

        Length = length;
        ZeroOnDispose = zeroOnDispose;
    }

    #region Properties

    /// <summary>
    /// Gets or sets the valid payload length within the owned slice.
    /// </summary>
    public int Length { get; private set; }

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
    public Span<byte> Span => _buffer is null ? Span<byte>.Empty : new Span<byte>(_buffer, _start, Length);

    /// <summary>
    /// Writable span over the full owned slice (capacity).
    /// </summary>
    [SuppressMessage("Style", "IDE0301:Simplify collection initialization", Justification = "<Pending>")]
    public Span<byte> SpanFull => _buffer is null ? Span<byte>.Empty : new Span<byte>(_buffer, _start, Capacity);

    /// <summary>
    /// Read-only view of the valid payload slice.
    /// </summary>
    public ReadOnlyMemory<byte> Memory => _buffer is null ? ReadOnlyMemory<byte>.Empty : new ReadOnlyMemory<byte>(_buffer, _start, Length);

    #endregion Properties

    #region APIs

#if DEBUG

    /// <summary>
    /// Convenient ArraySegment over the valid payload slice.
    /// </summary>
    [DebuggerStepThrough]
    [MethodImpl(
        MethodImplOptions.AggressiveInlining)]
    [return: NotNull]
    public ArraySegment<byte> AsSegment()
        => _buffer is null ? default : new ArraySegment<byte>(_buffer, _start, Length);

#endif

    /// <summary>
    /// Increases the reference count so multiple consumers can hold this lease safely.
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    [DebuggerStepThrough]
    [MethodImpl(
        MethodImplOptions.AggressiveInlining)]
    public void Retain()
    {
        ObjectDisposedException.ThrowIf(_buffer is null, nameof(BufferLease));

        int newValue = Interlocked.Increment(ref _refCount);

        if (newValue <= 1)
        {
            // newValue == 1: ok (single owner) — overflow -> negative or 0
            throw new InvalidOperationException(
                $"[{nameof(BufferLease)}] Invalid ref-count increment: {newValue}.");
        }
    }

    /// <summary>
    /// Sets the valid payload length (must be 0..Capacity).
    /// </summary>
    /// <param name="length"></param>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    [DebuggerStepThrough]
    [MethodImpl(
        MethodImplOptions.AggressiveInlining)]
    public void CommitLength([NotNull] int length)
    {
        if ((uint)length > (uint)Capacity)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        Length = length;
    }

    /// <summary>
    /// Releases a reference. When the count reaches zero and not detached, returns the array to <see cref="BufferPoolManager"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    [StackTraceHidden]
    [DebuggerStepThrough]
    [MethodImpl(
        MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        int newValue = Interlocked.Decrement(ref _refCount);

        if (newValue < 0)
        {
            throw new InvalidOperationException(
                $"[{nameof(BufferLease)}] Ref-count underflow. Dispose called too many times.");
        }

        if (newValue != 0)
        {
            return;
        }

        if (Volatile.Read(ref _detached) == 1)
        {
            _buffer = null;
            _start = 0;
            Length = 0;
            return;
        }

        byte[]? buf = Interlocked.Exchange(ref _buffer, null);
        int start = _start;
        int len = Length;

        _start = 0;
        Length = 0;

        if (buf is not null)
        {
            if (len > 0)
            {
                Span<byte> slice = new(buf, start, len);

                if (ZeroOnDispose)
                {
                    // Security first
                    slice.Clear();
                }
#if DEBUG
                if (EnablePoisonOnDispose)
                {
                    // Debugging aid – detect use-after-free
                    slice.Fill(PoisonByte);
                }
#endif
            }

            ByteArrayPool.Return(buf);
        }
    }

    /// <summary>
    /// Transfers ownership of the underlying array slice to the caller (no pool return on Dispose).
    /// After a successful release, this instance becomes empty and disposing it is a no-op.
    /// Only allowed when this is the last reference (refCount == 1).
    /// </summary>
    /// <param name="buffer"></param>
    /// <param name="start"></param>
    /// <param name="length"></param>
    [StackTraceHidden]
    [DebuggerStepThrough]
    [MethodImpl(
        MethodImplOptions.NoInlining)]
    [return: NotNull]
    public bool ReleaseOwnership(
        [MaybeNull] out byte[]? buffer,
        [NotNull] out int start,
        [NotNull] out int length)
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
        length = Length;

        _start = 0;
        Length = 0;
        return buffer is not null;
    }

    /// <summary>
    /// Auto-rents a buffer from <see cref="BufferPoolManager"/> and returns a new empty slice [start=0, length=0].
    /// Caller writes to <see cref="SpanFull"/> then calls <see cref="CommitLength(int)"/>.
    /// </summary>
    /// <param name="capacity"></param>
    /// <param name="zeroOnDispose"></param>
    [DebuggerStepThrough]
    [MethodImpl(
        MethodImplOptions.AggressiveInlining)]
    [return: NotNull]
    public static BufferLease Rent(
        [NotNull] int capacity,
        [NotNull] bool zeroOnDispose = false)
    {
        byte[] arr = ByteArrayPool.Rent(capacity);
        return new BufferLease(arr, start: 0, length: 0, zeroOnDispose: zeroOnDispose);
    }

    /// <summary>
    /// Creates a <see cref="BufferLease"/> by copying the source into a newly rented buffer.
    /// </summary>
    /// <param name="src"></param>
    /// <param name="zeroOnDispose"></param>
    [return: NotNull]
    public static BufferLease CopyFrom(
        [NotNull] ReadOnlySpan<byte> src,
        [NotNull] bool zeroOnDispose = false)
    {
        byte[] arr = ByteArrayPool.Rent(src.Length);
        src.CopyTo(MemoryExtensions.AsSpan(arr, 0, src.Length));
        return new BufferLease(arr, start: 0, length: src.Length, zeroOnDispose: zeroOnDispose);
    }

    /// <summary>
    /// Wraps an array that was previously rented from <see cref="BufferPoolManager"/> (payload starts at 0).
    /// Caller asserts the array comes from the same pool and is safe to own here.
    /// </summary>
    /// <param name="buffer"></param>
    /// <param name="length"></param>
    /// <param name="zeroOnDispose"></param>
    [MethodImpl(
        MethodImplOptions.AggressiveInlining)]
    [return: NotNull]
    public static BufferLease FromRented(
        [NotNull] byte[] buffer,
        [NotNull] int length,
        [NotNull] bool zeroOnDispose = false)
        => new(buffer, start: 0, length: length, zeroOnDispose: zeroOnDispose);

    /// <summary>
    /// Wraps a slice [<paramref name="start"/>..&lt;start+length&gt;) of a previously rented array from <see cref="BufferPoolManager"/>.
    /// This is the key API for zero-copy handoff of a payload located after a protocol header.
    /// </summary>
    /// <param name="buffer"></param>
    /// <param name="start"></param>
    /// <param name="length"></param>
    /// <param name="zeroOnDispose"></param>
    [MethodImpl(
        MethodImplOptions.AggressiveInlining)]
    [return: NotNull]
    public static BufferLease TakeOwnership(
        [NotNull] byte[] buffer,
        [NotNull] int start,
        [NotNull] int length,
        [NotNull] bool zeroOnDispose = false)
        => new(buffer, start, length, zeroOnDispose);

    #endregion APIs
}
