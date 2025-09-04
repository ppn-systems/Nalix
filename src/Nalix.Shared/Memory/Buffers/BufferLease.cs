// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Caching;
using Nalix.Shared.Injection;
using Nalix.Shared.Memory.Pooling;

namespace Nalix.Shared.Memory.Buffers;

/// <summary>
/// Represents an owned lease of a pooled System.Byte[] rented from <see cref="BufferPoolManager"/>.
/// Use <see cref="Rent"/> for auto-rent, <see cref="CopyFrom"/> to copy data into a new lease,
/// or <see cref="FromRented(System.Byte[], System.Int32, System.Boolean)"/> to wrap an already rented array.
/// The lease is reference-counted via <see cref="Retain"/>/<see cref="Dispose"/> and returns
/// the buffer to the pool on the final dispose, unless <see cref="TryDetach(out System.Byte[], out System.Int32)"/> is used.
/// </summary>
[System.Diagnostics.DebuggerDisplay("BufferLease Len={Length}, Cap={Capacity}, Detached={_detached != 0}")]
public sealed class BufferLease : IBufferLease
{
    private System.Byte[]? _buffer;
    private System.Int32 _refCount;
    private System.Int32 _detached; // 0 = no, 1 = yes

    /// <summary>
    /// Creates a new lease wrapping <paramref name="buffer"/>.
    /// </summary>
    private BufferLease(System.Byte[] buffer, System.Int32 length, System.Boolean zeroOnDispose)
    {
        _buffer = buffer ?? throw new System.ArgumentNullException(nameof(buffer));
        if ((System.UInt32)length > (System.UInt32)buffer.Length)
        {
            throw new System.ArgumentOutOfRangeException(nameof(length), "Length cannot exceed buffer length.");
        }

        Length = length;
        Capacity = buffer.Length;
        ZeroOnDispose = zeroOnDispose;

        _refCount = 1;
        _detached = 0;
    }

    /// <summary>
    /// Gets or sets the valid payload length within the underlying buffer.
    /// </summary>
    public System.Int32 Length { get; private set; }

    /// <summary>
    /// Gets the capacity (array length) of the underlying buffer.
    /// </summary>
    public System.Int32 Capacity { get; }

    /// <summary>
    /// Gets or sets whether the buffer should be zeroed before returning to the pool.
    /// </summary>
    public System.Boolean ZeroOnDispose { get; set; }

    /// <summary>
    /// Gets a read-only view of the valid payload.
    /// </summary>
    public System.ReadOnlyMemory<System.Byte> Memory => new(_buffer!, 0, Length);

    /// <summary>
    /// Gets a writable span covering <see cref="Length"/> bytes. 
    /// For writing beyond <see cref="Length"/>, call <see cref="SpanFull"/> then <see cref="SetLength(System.Int32)"/>.
    /// </summary>
    public System.Span<System.Byte> Span => new(_buffer, 0, Length);

    /// <summary>
    /// Gets a writable span covering the entire capacity.
    /// </summary>
    public System.Span<System.Byte> SpanFull => new(_buffer, 0, Capacity);

    /// <summary>
    /// The raw array (may be larger than <see cref="Length"/>). Prefer using <see cref="Span"/> and <see cref="Memory"/>.
    /// </summary>
    public System.Byte[]? RawArray => _buffer;

    /// <summary>
    /// Increases the reference count so multiple consumers can hold this lease safely.
    /// </summary>
    public void Retain() => System.Threading.Interlocked.Increment(ref _refCount);

    /// <summary>
    /// Sets the valid payload length (must be 0..Capacity).
    /// </summary>
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

        // Detached => ownership handed off elsewhere; drop reference only.
        if (System.Threading.Volatile.Read(ref _detached) == 1)
        {
            _buffer = null;
            Length = 0;
            return;
        }

        var buf = System.Threading.Interlocked.Exchange(ref _buffer, null);
        if (buf is not null)
        {
            if (ZeroOnDispose && Length > 0)
            {
                // Clear only the used portion for speed; set to Capacity if you prefer clearing full array.
                System.MemoryExtensions.AsSpan(buf, 0, Length).Clear();
            }

            InstanceManager.Instance.GetOrCreateInstance<BufferPoolManager>()
                                    .Return(buf);
        }
        Length = 0;
    }

    /// <summary>
    /// Transfers ownership of the underlying array to the caller (no pool return on Dispose).
    /// After a successful detach, this instance becomes empty and disposing it is a no-op.
    /// </summary>
    public System.Boolean TryDetach(out System.Byte[]? buffer, out System.Int32 length)
    {
        if (System.Threading.Interlocked.Exchange(ref _detached, 1) == 1)
        {
            buffer = null; length = 0;
            return false; // Already detached
        }

        buffer = System.Threading.Interlocked.Exchange(ref _buffer, null);
        length = Length;
        Length = 0;

        return buffer is not null;
    }

    /// <summary>
    /// Auto-rents a buffer from <see cref="BufferPoolManager"/> and returns a new lease.
    /// The lease starts with <see cref="Length"/> = 0 and <see cref="Capacity"/> = <paramref name="capacity"/>.
    /// </summary>
    public static BufferLease Rent(System.Int32 capacity, System.Boolean zeroOnDispose = false)
    {
        var pool = InstanceManager.Instance.GetOrCreateInstance<BufferPoolManager>();
        var arr = pool.Rent(capacity);
        // Start “empty”: caller writes to SpanFull and then SetLength()
        return new BufferLease(arr, length: 0, zeroOnDispose: zeroOnDispose);
    }

    /// <summary>
    /// Creates a <see cref="BufferLease"/> by copying the source into a newly rented pooled array.
    /// </summary>
    public static BufferLease CopyFrom(System.ReadOnlySpan<System.Byte> src, System.Boolean zeroOnDispose = false)
    {
        var pool = InstanceManager.Instance.GetOrCreateInstance<BufferPoolManager>();
        var arr = pool.Rent(src.Length);
        src.CopyTo(System.MemoryExtensions.AsSpan(arr, 0, src.Length));
        return new BufferLease(arr, src.Length, zeroOnDispose);
    }

    /// <summary>
    /// Wraps an array that was previously rented from <see cref="BufferPoolManager"/>.
    /// Caller asserts the array comes from the same pool and is safe to own here.
    /// </summary>
    public static BufferLease FromRented(System.Byte[] buffer, System.Int32 length,
        System.Boolean zeroOnDispose = false) => new(buffer, length, zeroOnDispose);
}
