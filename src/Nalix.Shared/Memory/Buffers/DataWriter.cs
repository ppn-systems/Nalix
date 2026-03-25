// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Shared.Memory.Buffers;

/// <summary>
/// A mutable, growable write buffer for high-performance serialization.
/// Internally stores a <see cref="System.Span{Byte}"/> view and, when rented,
/// keeps the backing <see cref="byte"/> array to allow expansion and pooling.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
[System.Diagnostics.DebuggerDisplay("Len={Length}, Written={WrittenCount}, Rent={_rent}, OWNER={( _owner != null )}")]
public struct DataWriter
{
    #region Fields

    /// <summary>
    /// Current writable view
    /// </summary>
    private System.Span<byte> _span;

    /// <summary>
    /// Backing array when owned (rented or external array); null if wrapping an external span
    /// </summary>
    private byte[]? _owner;

    /// <summary>
    /// True when the backing array was rented from ArrayPool and must be returned on Dispose
    /// </summary>
    private readonly bool _rent;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Creates a writer that rents its internal buffer from the shared array pool.
    /// </summary>
    /// <param name="size">Initial capacity in bytes (must be &gt; 0).</param>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown if <paramref name="size"/> is not positive.</exception>
    public DataWriter(int size)
    {
        if (size <= 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(size), "SIZE must be greater than zero.");
        }

        _rent = true;
        _owner = BufferLease.ByteArrayPool.Rent(size);
        System.Diagnostics.Debug.Assert(_owner.Length >= size, "ArrayPool returned insufficient buffer");
        _span = System.MemoryExtensions.AsSpan(_owner);

        WrittenCount = 0;
    }

    /// <summary>
    /// Creates a writer over an existing external array (no renting, no automatic return).
    /// </summary>
    /// <param name="buffer">External backing array (length must be &gt; 0).</param>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown if <paramref name="buffer"/> has zero length.</exception>
    public DataWriter(byte[] buffer)
    {
        if (buffer.Length == 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(buffer), "SIZE must be greater than zero.");
        }

        _rent = false;
        _owner = buffer;                 // owns an external array (but not rented) → cannot expand by rent policy
        _span = System.MemoryExtensions.AsSpan(buffer);

        WrittenCount = 0;
    }

    /// <summary>
    /// Creates a writer over an existing external array (no renting, no automatic return).
    /// </summary>
    /// <param name="span">External backing array (length must be &gt; 0).</param>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown if <paramref name="span"/> has zero length.</exception>
    public DataWriter(System.Span<byte> span)
    {
        if (span.Length <= 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(span), "SIZE must be greater than zero.");
        }

        _span = span;       // direct span view (stackalloc, sliced array, etc.)
        _owner = null;      // no backing array ownership
        _rent = false;      // not rented → cannot Expand()

        WrittenCount = 0;
    }

    #endregion Constructors

    #region Properties

    /// <summary>
    /// Gets the number of bytes committed via <see cref="Advance(int)"/>.
    /// </summary>
    [System.Diagnostics.Contracts.Pure]
    public int WrittenCount { get; private set; }

    /// <summary>
    /// Gets a span representing the remaining unwritten segment of the buffer.
    /// </summary>
    [System.Diagnostics.Contracts.Pure]
    public readonly System.Span<byte> FreeBuffer => _span[WrittenCount..];

    #endregion Properties

    #region APIs

    /// <summary>
    /// Advances the write cursor by the specified count.
    /// </summary>
    /// <param name="count">Number of bytes to commit (must fit into <see cref="FreeBuffer"/>).</param>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown if out of bounds or non-positive.</exception>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Advance(int count)
    {
        if (count <= 0 || (uint)(WrittenCount + count) > (uint)_span.Length)
        {
            throw new System.ArgumentOutOfRangeException(nameof(count), "Advance out of buffer bounds.");
        }

        WrittenCount += count;
    }

    /// <summary>
    /// Returns a reference to the first byte of <see cref="FreeBuffer"/> for ref-based writes.
    /// Call <see cref="Expand(int)"/> beforehand to ensure capacity.
    /// </summary>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Diagnostics.CodeAnalysis.UnscopedRef]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public readonly ref byte GetFreeBufferReference() => ref System.Runtime.InteropServices.MemoryMarshal.GetReference(FreeBuffer);

    /// <summary>
    /// Ensures at least <paramref name="minimumSize"/> bytes are available in <see cref="FreeBuffer"/>.
    /// Expands only when this instance owns a rented array; throws for fixed/external buffers.
    /// </summary>
    /// <param name="minimumSize">Required free space in bytes (must be &gt; 0).</param>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown if non-positive.</exception>
    /// <exception cref="System.InvalidOperationException">Thrown when expansion is not allowed.</exception>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void Expand(int minimumSize)
    {
        if (minimumSize <= 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(minimumSize), "SIZE must be greater than zero.");
        }

        if (_span.Length - WrittenCount >= minimumSize)
        {
            return;
        }

        if (!_rent) // external array (or external span if we ever add such ctor) cannot expand by policy
        {
            throw new System.InvalidOperationException("Cannot expand a fixed buffer.");
        }

        // Rent a larger buffer and copy committed bytes
        int current = _owner?.Length ?? 0;
        int needed = WrittenCount + minimumSize;
        int newSize = current <= 0 ? needed : System.Math.Max(current * 2, needed);

        byte[] newOwner = BufferLease.ByteArrayPool.Rent(newSize);
        if (WrittenCount > 0)
        {
            if (current <= 128)
            {
                _span[..WrittenCount].CopyTo(newOwner);
            }
            else
            {
                CopyBytes(_owner, newOwner, WrittenCount);
            }
        }

        byte[]? oldOwner = _owner;

        _owner = newOwner;
        _span = System.MemoryExtensions.AsSpan(_owner);

        if (oldOwner is not null)
        {
            BufferLease.ByteArrayPool.Return(oldOwner);
        }

        [System.Diagnostics.StackTraceHidden]
        [System.Diagnostics.DebuggerStepThrough]
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        static unsafe void CopyBytes(byte[]? src, byte[] dst, int count)
        {
            System.ArgumentNullException.ThrowIfNull(src);
            System.ArgumentNullException.ThrowIfNull(dst);
            System.ArgumentOutOfRangeException.ThrowIfNegative(count);

            if ((uint)count > (uint)src.Length || (uint)count > (uint)dst.Length)
            {
                throw new System.ArgumentOutOfRangeException(nameof(count));
            }

            fixed (byte* pSrc = &System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(src))
            {
                fixed (byte* pDst = &System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(dst))
                {
                    System.Buffer.MemoryCopy(pSrc, pDst, (nuint)count, (nuint)count);
                }
            }
        }
    }

    /// <summary>
    /// Copies the committed data into a new tightly sized array.
    /// </summary>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    public readonly byte[] ToArray()
    {
        int n = WrittenCount;
        byte[] result = new byte[n];
        if (n > 0)
        {
            _span[..n].CopyTo(result);
        }

        return result;
    }

    /// <summary>
    /// Clears state and returns the rented array to the pool when applicable.
    /// </summary>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (_owner is not null)
        {
            if (_rent)
            {
                BufferLease.ByteArrayPool.Return(_owner);
            }

            _owner = null;
        }

        _span = [];
        WrittenCount = 0;
    }

    #endregion APIs
}
