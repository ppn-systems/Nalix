// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Nalix.Abstractions.Exceptions;
using Nalix.Codec.Internal;

namespace Nalix.Codec.Memory;

/// <summary>
/// Provides functionality for reading serialized data from a byte buffer.
/// Supports managed <c>byte[]</c>, <c>ReadOnlyMemory&lt;byte&gt;</c>, <c>ReadOnlySpan&lt;byte&gt;</c>, and unmanaged memory.
/// </summary>
[DebuggerNonUserCode]
[SkipLocalsInit]
[ExcludeFromCodeCoverage]
[EditorBrowsable(EditorBrowsableState.Never)]
[DebuggerDisplay("Len={_buffer.Length}, Read={BytesRead}, Rem={BytesRemaining}")]
public ref struct DataReader
{
    #region Fields

    private ReadOnlySpan<byte> _buffer;
    private int _pos;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the number of bytes that have been consumed from the buffer.
    /// </summary>
    public readonly int BytesRead
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _pos;
    }

    /// <summary>
    /// Gets the number of bytes remaining in the buffer.
    /// </summary>
    public readonly int BytesRemaining
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _buffer.Length - _pos;
    }

    #endregion Properties

    #region Constructors

    /// <summary>
    /// Initializes a new instance of <see cref="DataReader"/> for a managed byte array.
    /// </summary>
    /// <param name="buffer">The byte array to read from.</param>
    public DataReader(byte[] buffer)
    {
        _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        _pos = 0;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="DataReader"/> for an unmanaged memory pointer.
    /// </summary>
    /// <param name="ptr">The unmanaged memory pointer.</param>
    /// <param name="length">The length of the buffer.</param>
    [SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "<Pending>")]
    public unsafe DataReader(byte* ptr, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        _buffer = new ReadOnlySpan<byte>(ptr, length);
        _pos = 0;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="DataReader"/> for a read-only span of bytes.
    /// </summary>
    /// <param name="span">The read-only span of bytes to read from.</param>
    public DataReader(ReadOnlySpan<byte> span)
    {
        _buffer = span;
        _pos = 0;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="DataReader"/> for a read-only memory buffer.
    /// </summary>
    /// <param name="memory">The read-only memory of bytes to read from.</param>
    public DataReader(ReadOnlyMemory<byte> memory)
    {
        _buffer = memory.Span;
        _pos = 0;
    }

    #endregion Constructors

    #region APIs

    /// <summary>
    /// Retrieves a reference to the first byte in the requested span.
    /// </summary>
    /// <param name="sizeHint">The minimum number of bytes required.</param>
    /// <returns>A reference to the first byte of the span.</returns>
    /// <exception cref="SerializationFailureException">
    /// Thrown if the requested size exceeds the available buffer size.
    /// </exception>
    [Pure]
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ref byte GetSpanReference(int sizeHint)
    {
        if ((uint)sizeHint > (uint)this.BytesRemaining)
        {
            Throw.EndOfStream();
        }

        return ref MemoryMarshal.GetReference(_buffer[_pos..]);
    }

    /// <summary>
    /// Advances the read position in the buffer by the specified number of bytes.
    /// </summary>
    /// <param name="count">The number of bytes to advance.</param>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (count > this.BytesRemaining)
        {
            Throw.EndOfStream();
        }

        _pos += count;
    }

    /// <summary>
    /// Resets the reader state.
    /// </summary>
    [StackTraceHidden]
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        _buffer = default;
        _pos = 0;
    }

    #endregion APIs
}
