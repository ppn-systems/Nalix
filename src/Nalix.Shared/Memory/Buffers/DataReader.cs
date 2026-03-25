// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Exceptions;

namespace Nalix.Shared.Memory.Buffers;

/// <summary>
/// Provides functionality for reading serialized data from a byte buffer.
/// Supports managed <c>byte[]</c>, <c>ReadOnlyMemory&lt;byte&gt;</c>, <c>ReadOnlySpan&lt;byte&gt;</c>, and unmanaged memory.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
[System.Diagnostics.DebuggerDisplay("Len={_length}, Read={BytesRead}, Rem={BytesRemaining}, Pinned={_pinned}")]
public unsafe struct DataReader : System.IDisposable
{
    #region Fields

    private byte* _ptr;
    private bool _pinned;
    private readonly byte[]? _tempArray;

    /// <summary>
    /// Used only when the source is a byte array
    /// </summary>
    private System.Runtime.InteropServices.GCHandle _pin;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the number of bytes that have been consumed from the buffer.
    /// </summary>
    public int BytesRead { get; private set; }

    /// <summary>
    /// Gets the number of bytes remaining in the buffer.
    /// </summary>
    public int BytesRemaining { readonly get => field - BytesRead; private set; }

    #endregion Properties

    #region Constructors

    /// <summary>
    /// Initializes a new instance of <see cref="DataReader"/> for a managed byte array.
    /// This ensures safety by pinning the array to prevent movement by the garbage collector.
    /// </summary>
    /// <param name="buffer">The byte array to read from.</param>
    /// <exception cref="System.ArgumentNullException">Thrown if the provided buffer is null.</exception>
    public DataReader(byte[] buffer)
    {
        System.ArgumentNullException.ThrowIfNull(buffer);
        _pin = System.Runtime.InteropServices.GCHandle.Alloc(buffer, System.Runtime.InteropServices.GCHandleType.Pinned);
        _ptr = (byte*)_pin.AddrOfPinnedObject();
        BytesRemaining = buffer.Length;
        _pinned = true;

        BytesRead = 0;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="DataReader"/> for an unmanaged memory pointer.
    /// This is useful for reading from native memory, stack allocations, or manually allocated buffers.
    /// </summary>
    /// <param name="ptr">The unmanaged memory pointer.</param>
    /// <param name="length">The length of the buffer.</param>
    public DataReader(byte* ptr, int length)
    {
        BytesRead = 0;

        _ptr = ptr;
        BytesRemaining = length;
        _pin = default;
        _pinned = false;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="DataReader"/> for a read-only span of bytes.
    /// This constructor creates a pinned copy of the span to ensure safe access to its data.
    /// </summary>
    /// <param name="span">The read-only span of bytes to read from.</param>
    public DataReader(System.ReadOnlySpan<byte> span)
    {
        _tempArray = span.ToArray();
        _pin = System.Runtime.InteropServices.GCHandle.Alloc(_tempArray, System.Runtime.InteropServices.GCHandleType.Pinned);
        _ptr = (byte*)_pin.AddrOfPinnedObject();
        BytesRemaining = _tempArray.Length;
        _pinned = true; // Fixed!

        BytesRead = 0;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="DataReader"/> for a read-only span of bytes.
    /// This constructor creates a pinned copy of the span to ensure safe access to its data.
    /// </summary>
    /// <param name="memory">The read-only memory of bytes to read from.</param>
    public DataReader(System.ReadOnlyMemory<byte> memory)
    {
        if (System.Runtime.InteropServices.MemoryMarshal.TryGetArray(memory, out System.ArraySegment<byte> segment))
        {
            _pin = System.Runtime.InteropServices.GCHandle.Alloc(segment.Array, System.Runtime.InteropServices.GCHandleType.Pinned);
            _ptr = (byte*)_pin.AddrOfPinnedObject() + segment.Offset;
            BytesRemaining = segment.Count;
            _pinned = true;
        }
        else
        {
            // fallback: allocate + copy
            byte[] temp = memory.ToArray();
            _pin = System.Runtime.InteropServices.GCHandle.Alloc(temp, System.Runtime.InteropServices.GCHandleType.Pinned);
            _ptr = (byte*)_pin.AddrOfPinnedObject();
            BytesRemaining = temp.Length;
            _pinned = true;
        }

        BytesRead = 0;
    }

    #endregion Constructors

    #region APIs

    /// <summary>
    /// Retrieves a reference to the first byte in the requested span.
    /// </summary>
    /// <param name="sizeHint">The minimum number of bytes required.</param>
    /// <returns>A reference to the first byte of the span.</returns>
    /// <exception cref="SerializationException">
    /// Thrown if the requested size exceeds the available buffer size.
    /// </exception>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public readonly ref byte GetSpanReference(int sizeHint)
    {
        if (sizeHint > BytesRemaining)
        {
            throw new SerializationException(
                $"Not enough data: requested {sizeHint} bytes, only {BytesRemaining} bytes remaining.");
        }

        return ref *(_ptr + BytesRead);
    }

    /// <summary>
    /// Advances the read position in the buffer by the specified number of bytes.
    /// </summary>
    /// <param name="count">The number of bytes to advance.</param>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown if the advance count is negative.</exception>
    /// <exception cref="SerializationException">
    /// Thrown if the advance count exceeds the available buffer size.
    /// </exception>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Advance(int count)
    {
        System.ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (count > BytesRemaining)
        {
            throw new SerializationException(
                $"Cannot advance {count} bytes, only {BytesRemaining} bytes remaining.");
        }

        BytesRead += count;
    }

    /// <summary>
    /// Releases pinned memory if necessary and resets the reader state.
    /// </summary>
    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (_pinned)
        {
            _pin.Free();
        }

        _ptr = null;
        BytesRemaining = 0;
        _pinned = false;

        BytesRead = 0;
    }

    #endregion APIs
}
