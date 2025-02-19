// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Caching;
using Nalix.Framework.Injection;
using Nalix.Shared.Memory.Pooling;

namespace Nalix.Shared.Memory.Buffers;

/// <summary>
/// Represents an owned lease of a pooled byte[] rented from <see cref="BufferPoolManager"/>.
/// Supports slice ownership (start + length) to enable zero-copy handoffs.
/// </summary>
[System.Diagnostics.DebuggerDisplay("BufferLease Start={_start}, Len={Length}, Cap={Capacity}, Detached={_detached != 0}")]
public sealed class BufferLease : IBufferLease
{
    // ====== Static ======

    /// <summary>
    /// Gets the shared <see cref="BufferPoolManager"/> instance used for buffer pooling.
    /// </summary>
    public static readonly BufferPoolManager Pool = InstanceManager.Instance.GetOrCreateInstance<BufferPoolManager>();

    // ====== Fields ======
    private System.Byte[]? _buffer;
    private System.Int32 _start;                    // slice start (>= 0, <= RawCapacity)
    private System.Int32 _refCount;                 // reference count (>= 0)
    private System.Int32 _detached;                 // 0 = no, 1 = yes

    // ====== Ctor ======
    private BufferLease(System.Byte[] buffer, System.Int32 start, System.Int32 length, System.Boolean zeroOnDispose)
    {
        System.ArgumentNullException.ThrowIfNull(buffer);

        if ((System.UInt32)start > (System.UInt32)buffer.Length)
        {
            throw new System.ArgumentOutOfRangeException(nameof(start));
        }

        if ((System.UInt32)length > (System.UInt32)(buffer.Length - start))
        {
            throw new System.ArgumentOutOfRangeException(nameof(length));
        }

        _buffer = buffer;
        _start = start;
        Length = length;
        ZeroOnDispose = zeroOnDispose;
        _refCount = 1;
        _detached = 0;
    }

    #region Properties

    /// <summary>
    /// Gets or sets the valid payload length within the owned slice.
    /// </summary>
    public System.Int32 Length { get; private set; }

    /// <summary>
    /// Gets the capacity of the owned slice (from <c>_start</c> to end of the array).
    /// </summary>
    public System.Int32 Capacity => _buffer is null ? 0 : _buffer.Length - _start;

    /// <summary>
    /// Gets the total capacity (underlying array length).
    /// </summary>
    public System.Int32 RawCapacity => _buffer?.Length ?? 0;

    /// <summary>
    /// Gets or sets whether the slice should be zeroed before returning to the pool.
    /// </summary>
    public System.Boolean ZeroOnDispose { get; set; }

    /// <summary>
    /// The raw array (may be null after dispose/detach). Prefer using <see cref="Span"/>/<see cref="Memory"/>.
    /// </summary>
    public System.Byte[]? RawArray => _buffer;

    /// <summary>
    /// Read-only view of the valid payload slice.
    /// </summary>
    public System.ReadOnlyMemory<System.Byte> Memory
        => _buffer is null ? System.ReadOnlyMemory<System.Byte>.Empty : new System.ReadOnlyMemory<System.Byte>(_buffer, _start, Length);

    /// <summary>
    /// Writable span over the valid payload slice.
    /// </summary>
    public System.Span<System.Byte> Span
        => _buffer is null ? [] : new System.Span<System.Byte>(_buffer, _start, Length);

    /// <summary>
    /// Writable span over the full owned slice (capacity).
    /// </summary>
    public System.Span<System.Byte> SpanFull
        => _buffer is null ? [] : new System.Span<System.Byte>(_buffer, _start, Capacity);

    #endregion Properties

    #region APIs

    /// <summary>
    /// Convenient ArraySegment over the valid payload slice.
    /// </summary>
    public System.ArraySegment<System.Byte> AsSegment()
        => _buffer is null ? default : new System.ArraySegment<System.Byte>(_buffer, _start, Length);

    /// <summary>
    /// Increases the reference count so multiple consumers can hold this lease safely.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Retain()
    {
        System.ObjectDisposedException.ThrowIf(_buffer is null, nameof(BufferLease));

        _ = System.Threading.Interlocked.Increment(ref _refCount);
    }

    /// <summary>
    /// Sets the valid payload length (must be 0..Capacity).
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void SetLength(System.Int32 length)
    {
        if ((System.UInt32)length > (System.UInt32)Capacity)
        {
            throw new System.ArgumentOutOfRangeException(nameof(length));
        }

        Length = length;
    }

    /// <summary>
    /// Releases a reference. When the count reaches zero and not detached, returns the array to <see cref="BufferPoolManager"/>.
    /// </summary>
    public void Dispose()
    {
        if (System.Threading.Interlocked.Decrement(ref _refCount) != 0)
        {
            return;
        }

        // If detached, drop reference only
        if (System.Threading.Volatile.Read(ref _detached) == 1)
        {
            _buffer = null;
            _start = 0;
            Length = 0;
            return;
        }

        var buf = System.Threading.Interlocked.Exchange(ref _buffer, null);
        var start = _start;
        var len = Length;
        _start = 0;
        Length = 0;

        if (buf is not null)
        {
            if (ZeroOnDispose && len > 0)
            {
                // Clear only the used region of the slice for speed.
                new System.Span<System.Byte>(buf, start, len).Clear();
            }
            Pool.Return(buf);
        }
    }

    /// <summary>
    /// Transfers ownership of the underlying array slice to the caller (no pool return on Dispose).
    /// After a successful detach, this instance becomes empty and disposing it is a no-op.
    /// Only allowed when this is the last reference (refCount == 1).
    /// </summary>
    public System.Boolean TryDetach(out System.Byte[]? buffer, out System.Int32 start, out System.Int32 length)
    {
        // Ensure single-owner detach (avoid breaking other holders)
        if (System.Threading.Volatile.Read(ref _refCount) != 1)
        {
            buffer = null; start = 0; length = 0;
            return false;
        }

        if (System.Threading.Interlocked.Exchange(ref _detached, 1) == 1)
        {
            buffer = null; start = 0; length = 0;
            return false; // Already detached
        }

        buffer = System.Threading.Interlocked.Exchange(ref _buffer, null);
        start = _start;
        length = Length;

        _start = 0;
        Length = 0;
        return buffer is not null;
    }

    /// <summary>
    /// Auto-rents a buffer from <see cref="BufferPoolManager"/> and returns a new empty slice [start=0, length=0].
    /// Caller writes to <see cref="SpanFull"/> then calls <see cref="SetLength(System.Int32)"/>.
    /// </summary>
    public static BufferLease Rent(System.Int32 capacity, System.Boolean zeroOnDispose = false)
    {
        var arr = Pool.Rent(capacity);
        return new BufferLease(arr, start: 0, length: 0, zeroOnDispose: zeroOnDispose);
    }

    /// <summary>
    /// Creates a <see cref="BufferLease"/> by copying the source into a newly rented buffer.
    /// </summary>
    public static BufferLease CopyFrom(System.ReadOnlySpan<System.Byte> src, System.Boolean zeroOnDispose = false)
    {
        var arr = Pool.Rent(src.Length);
        src.CopyTo(System.MemoryExtensions.AsSpan(arr, 0, src.Length));
        return new BufferLease(arr, start: 0, length: src.Length, zeroOnDispose: zeroOnDispose);
    }

    /// <summary>
    /// Wraps an array that was previously rented from <see cref="BufferPoolManager"/> (payload starts at 0).
    /// Caller asserts the array comes from the same pool and is safe to own here.
    /// </summary>
    public static BufferLease FromRented(System.Byte[] buffer, System.Int32 length, System.Boolean zeroOnDispose = false)
        => new(buffer, start: 0, length: length, zeroOnDispose: zeroOnDispose);

    /// <summary>
    /// Wraps a slice [<paramref name="start"/>..&lt;start+length&gt;) of a previously rented array from <see cref="BufferPoolManager"/>.
    /// This is the key API for zero-copy handoff of a payload located after a protocol header.
    /// </summary>
    public static BufferLease FromRentedSlice(System.Byte[] buffer, System.Int32 start, System.Int32 length, System.Boolean zeroOnDispose = false)
        => new(buffer, start, length, zeroOnDispose);

    /// <summary>
    /// Alias for <see cref="FromRentedSlice"/> for readability at call sites performing ownership transfer.
    /// </summary>
    public static BufferLease TakeOwnership(System.Byte[] buffer, System.Int32 start, System.Int32 length, System.Boolean zeroOnDispose = false)
        => new(buffer, start, length, zeroOnDispose);

    #endregion Properties
}
