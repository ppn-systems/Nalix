// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Nalix.Common.Exceptions;

namespace Nalix.Framework.Memory.Buffers;

/// <summary>
/// Provides functionality for reading serialized data from a byte buffer.
/// Supports managed <c>byte[]</c>, <c>ReadOnlyMemory&lt;byte&gt;</c>, <c>ReadOnlySpan&lt;byte&gt;</c>, and unmanaged memory.
/// </summary>
[DebuggerNonUserCode]
[SkipLocalsInit]
[ExcludeFromCodeCoverage]
[EditorBrowsable(EditorBrowsableState.Never)]
[StructLayout(LayoutKind.Sequential)]
[DebuggerDisplay("Len={_length}, Read={BytesRead}, Rem={BytesRemaining}, Pinned={_pinned}")]
public unsafe struct DataReader : IDisposable
{
    #region Fields

    private byte* _ptr;
    private bool _pinned;
    private readonly byte[]? _tempArray;

    /// <summary>
    /// Used only when the source is a byte array
    /// </summary>
    private GCHandle _pin;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the number of bytes that have been consumed from the buffer.
    /// </summary>
    public int BytesRead { get; private set; }

    /// <summary>
    /// Gets the number of bytes remaining in the buffer.
    /// </summary>
    public int BytesRemaining { readonly get => field - this.BytesRead; private set; }

    #endregion Properties

    #region Constructors

    /// <summary>
    /// Initializes a new instance of <see cref="DataReader"/> for a managed byte array.
    /// This ensures safety by pinning the array to prevent movement by the garbage collector.
    /// </summary>
    /// <param name="buffer">The byte array to read from.</param>
    /// <exception cref="ArgumentNullException">Thrown if the provided buffer is null.</exception>
    public DataReader(byte[] buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        _pin = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        _ptr = (byte*)_pin.AddrOfPinnedObject();
        this.BytesRemaining = buffer.Length;
        _pinned = true;

        this.BytesRead = 0;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="DataReader"/> for an unmanaged memory pointer.
    /// This is useful for reading from native memory, stack allocations, or manually allocated buffers.
    /// </summary>
    /// <param name="ptrB">The unmanaged memory pointer.</param>
    /// <param name="length">The length of the buffer.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="length"/> is negative.</exception>
    public DataReader(byte* ptrB, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        this.BytesRead = 0;

        _ptr = ptrB;
        this.BytesRemaining = length;
        _pin = default;
        _pinned = false;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="DataReader"/> for a read-only span of bytes.
    /// This constructor creates a pinned copy of the span to ensure safe access to its data.
    /// </summary>
    /// <param name="span">The read-only span of bytes to read from.</param>
    /// <exception cref="OutOfMemoryException">Thrown when the span copy cannot be allocated.</exception>
    public DataReader(ReadOnlySpan<byte> span)
    {
        _tempArray = span.ToArray();
        _pin = GCHandle.Alloc(_tempArray, GCHandleType.Pinned);
        _ptr = (byte*)_pin.AddrOfPinnedObject();
        this.BytesRemaining = _tempArray.Length;
        _pinned = true; // Fixed!

        this.BytesRead = 0;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="DataReader"/> for a read-only span of bytes.
    /// This constructor creates a pinned copy of the span to ensure safe access to its data.
    /// </summary>
    /// <param name="memory">The read-only memory of bytes to read from.</param>
    /// <exception cref="OutOfMemoryException">Thrown when the fallback copy cannot be allocated.</exception>
    public DataReader(ReadOnlyMemory<byte> memory)
    {
        if (MemoryMarshal.TryGetArray(memory, out ArraySegment<byte> segment))
        {
            _pin = GCHandle.Alloc(segment.Array, GCHandleType.Pinned);
            _ptr = (byte*)_pin.AddrOfPinnedObject() + segment.Offset;
            this.BytesRemaining = segment.Count;
            _pinned = true;
        }
        else
        {
            // fallback: allocate + copy
            byte[] temp = memory.ToArray();
            _pin = GCHandle.Alloc(temp, GCHandleType.Pinned);
            _ptr = (byte*)_pin.AddrOfPinnedObject();
            this.BytesRemaining = temp.Length;
            _pinned = true;
        }

        this.BytesRead = 0;
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
    [Pure]
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ref byte GetSpanReference(int sizeHint)
    {
        if (sizeHint > this.BytesRemaining)
        {
            throw new SerializationException(
                $"Not enough data: requested {sizeHint} bytes, only {this.BytesRemaining} bytes remaining.");
        }

        return ref *(_ptr + this.BytesRead);
    }

    /// <summary>
    /// Advances the read position in the buffer by the specified number of bytes.
    /// </summary>
    /// <param name="count">The number of bytes to advance.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the advance count is negative.</exception>
    /// <exception cref="SerializationException">
    /// Thrown if the advance count exceeds the available buffer size.
    /// </exception>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (count > this.BytesRemaining)
        {
            throw new SerializationException(
                $"Cannot advance {count} bytes, only {this.BytesRemaining} bytes remaining.");
        }

        this.BytesRead += count;
    }

    /// <summary>
    /// Releases pinned memory if necessary and resets the reader state.
    /// </summary>
    [StackTraceHidden]
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (_pinned)
        {
            _pin.Free();
        }

        _ptr = null;
        this.BytesRemaining = 0;
        _pinned = false;

        this.BytesRead = 0;
    }

    #endregion APIs
}
