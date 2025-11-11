// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Shared.Memory.Buffers;

/// <summary>
/// A mutable, growable write buffer for high-performance serialization.
/// Internally stores a <see cref="System.Span{Byte}"/> view and, when rented,
/// keeps the backing <see cref="System.Byte"/> array to allow expansion and pooling.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
[System.Diagnostics.DebuggerDisplay("Len={Length}, Written={WrittenCount}, Rent={_rent}, Owner={( _owner != null )}")]
public ref struct DataWriter
{
    #region Fields

    // Current writable view
    private System.Span<System.Byte> _span;

    // Backing array when owned (rented or external array); null if wrapping an external span
    private System.Byte[]? _owner;

    // True when the backing array was rented from ArrayPool and must be returned on Dispose
    private readonly System.Boolean _rent;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Creates a writer that rents its internal buffer from the shared array pool.
    /// </summary>
    /// <param name="size">Initial capacity in bytes (must be &gt; 0).</param>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown if <paramref name="size"/> is not positive.</exception>
    public DataWriter(System.Int32 size)
    {
        if (size <= 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(size), "Size must be greater than zero.");
        }

        _owner = System.Buffers.ArrayPool<System.Byte>.Shared.Rent(size);
        _span = System.MemoryExtensions.AsSpan(_owner);
        _rent = true;
        WrittenCount = 0;
    }

    /// <summary>
    /// Creates a writer over an existing external array (no renting, no automatic return).
    /// </summary>
    /// <param name="buffer">External backing array (length must be &gt; 0).</param>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown if <paramref name="buffer"/> has zero length.</exception>
    public DataWriter(System.Byte[] buffer)
    {
        if (buffer.Length == 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(buffer), "Size must be greater than zero.");
        }

        _owner = buffer;                 // owns an external array (but not rented) → cannot expand by rent policy
        _span = System.MemoryExtensions.AsSpan(buffer);
        _rent = false;
        WrittenCount = 0;
    }

    /// <summary>
    /// Creates a writer over an existing external array (no renting, no automatic return).
    /// </summary>
    /// <param name="span">External backing array (length must be &gt; 0).</param>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown if <paramref name="span"/> has zero length.</exception>
    public DataWriter(System.Span<System.Byte> span)
    {
        if (span.Length <= 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(span), "Size must be greater than zero.");
        }

        _owner = null;      // no backing array ownership
        _span = span;       // direct span view (stackalloc, sliced array, etc.)
        _rent = false;      // not rented → cannot Expand()
        WrittenCount = 0;
    }

    #endregion Constructors

    #region Properties

    /// <summary>
    /// Gets the number of bytes committed via <see cref="Advance(System.Int32)"/>.
    /// </summary>
    [System.Diagnostics.Contracts.Pure]
    public System.Int32 WrittenCount { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the buffer view is empty (no underlying storage).
    /// </summary>
    [System.Diagnostics.Contracts.Pure]
    public readonly System.Boolean IsNull => _span == System.Span<System.Byte>.Empty;

    /// <summary>
    /// Gets the total capacity of the internal buffer in bytes.
    /// </summary>
    [System.Diagnostics.Contracts.Pure]
    public readonly System.Int32 Length => _span.Length;

    /// <summary>
    /// Gets a span representing the remaining unwritten segment of the buffer.
    /// </summary>
    [System.Diagnostics.Contracts.Pure]
    public readonly System.Span<System.Byte> FreeBuffer => _span[WrittenCount..];

    /// <summary>
    /// Gets a span representing the committed (written) segment of the buffer.
    /// </summary>
    [System.Diagnostics.Contracts.Pure]
    public readonly System.Span<System.Byte> WrittenBuffer => _span[..WrittenCount];

    /// <summary>
    /// Gets a <see cref="System.Memory{Byte}"/> view over the committed data
    /// when a backing array exists; otherwise <see cref="System.Memory{Byte}.Empty"/>.
    /// </summary>
    [System.Diagnostics.Contracts.Pure]
    public readonly System.Memory<System.Byte> WrittenMemory
        => _owner is null ? System.Memory<System.Byte>.Empty
        : System.MemoryExtensions.AsMemory(_owner, 0, WrittenCount);

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
    public void Advance(System.Int32 count)
    {
        if (count <= 0 || (System.UInt32)(WrittenCount + count) > (System.UInt32)_span.Length)
        {
            throw new System.ArgumentOutOfRangeException(nameof(count), "Advance out of buffer bounds.");
        }

        WrittenCount += count;
    }

    /// <summary>
    /// Returns a reference to the first byte of <see cref="FreeBuffer"/> for ref-based writes.
    /// Call <see cref="Expand(System.Int32)"/> beforehand to ensure capacity.
    /// </summary>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Diagnostics.CodeAnalysis.UnscopedRef]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public readonly ref System.Byte GetFreeBufferReference()
        => ref System.Runtime.InteropServices.MemoryMarshal.GetReference(FreeBuffer);

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
    public void Expand(System.Int32 minimumSize)
    {
        [System.Diagnostics.StackTraceHidden]
        [System.Diagnostics.DebuggerStepThrough]
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        static unsafe void CopyBytes(System.Byte[]? src, System.Byte[] dst, System.Int32 count)
        {
            System.ArgumentNullException.ThrowIfNull(src);
            System.ArgumentNullException.ThrowIfNull(dst);
            System.ArgumentOutOfRangeException.ThrowIfNegative(count);

            if ((System.UInt32)count > (System.UInt32)src.Length || (System.UInt32)count > (System.UInt32)dst.Length)
            {
                throw new System.ArgumentOutOfRangeException(nameof(count));
            }

            fixed (System.Byte* pSrc = &System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(src))
            {
                fixed (System.Byte* pDst = &System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(dst))
                {
                    System.Buffer.MemoryCopy(pSrc, pDst, (System.UIntPtr)count, (System.UIntPtr)count);
                }
            }
        }

        if (minimumSize <= 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(minimumSize), "Size must be greater than zero.");
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
        System.Int32 current = _owner?.Length ?? 0;
        System.Int32 needed = WrittenCount + minimumSize;
        System.Int32 newSize = current <= 0 ? needed : System.Math.Max(current * 2, needed);

        System.Byte[] newOwner = System.Buffers.ArrayPool<System.Byte>.Shared.Rent(newSize);
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

        if (_owner is not null)
        {
            System.Buffers.ArrayPool<System.Byte>.Shared.Return(_owner);
        }

        _owner = newOwner;
        _span = System.MemoryExtensions.AsSpan(_owner);
    }

    /// <summary>
    /// Copies the committed data into a new tightly sized array.
    /// </summary>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    public readonly System.Byte[] ToArray()
    {
        System.Int32 n = WrittenCount;
        System.Byte[] result = new System.Byte[n];
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
                System.Buffers.ArrayPool<System.Byte>.Shared.Return(_owner);
            }

            _owner = null;
        }

        _span = [];
        WrittenCount = 0;
    }

    #endregion APIs
}
