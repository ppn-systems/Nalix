// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Buffers;
using Nalix.Environment.Memory;

namespace Nalix.Observability.Handlers;

/// <summary>
/// A lightweight class-allocated buffer writer that rents from <see cref="BufferLease.ByteArrayPool"/>
/// and allows extracting the written data directly into a <see cref="BufferLease"/> without allocation.
/// </summary>
public sealed class PooledBufferWriter : IBufferWriter<byte>, IDisposable
{
    private byte[]? _buffer;
    private int _index;

    /// <summary>
    /// Initializes a new instance of the <see cref="PooledBufferWriter"/> class with a default initial capacity.
    /// </summary>
    public PooledBufferWriter()
    {
        _buffer = BufferLease.ByteArrayPool.Rent(256);
        _index = 0;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PooledBufferWriter"/> class with the specified initial capacity.
    /// </summary>
    /// <param name="initialCapacity">The initial capacity of the rented buffer.</param>
    public PooledBufferWriter(int initialCapacity)
    {
        _buffer = BufferLease.ByteArrayPool.Rent(initialCapacity <= 0 ? 256 : initialCapacity);
        _index = 0;
    }

    /// <summary>
    /// Gets the number of bytes written to the buffer.
    /// </summary>
    public int WrittenCount => _index;

    /// <summary>
    /// Gets the total capacity of the current buffer.
    /// </summary>
    public int Capacity => _buffer?.Length ?? 0;

    /// <summary>
    /// Gets a read-only span over the written bytes in the buffer.
    /// </summary>
    public ReadOnlySpan<byte> WrittenSpan => _buffer is null ? ReadOnlySpan<byte>.Empty : _buffer.AsSpan(0, _index);

    /// <summary>
    /// Gets a read-only memory over the written bytes in the buffer.
    /// </summary>
    public ReadOnlyMemory<byte> WrittenMemory => _buffer is null ? ReadOnlyMemory<byte>.Empty : _buffer.AsMemory(0, _index);

    /// <summary>
    /// Advances the current index by the specified count.
    /// </summary>
    /// <param name="count">The number of bytes written.</param>
    /// <exception cref="ObjectDisposedException">Thrown if the writer has been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="count"/> is negative.</exception>
    /// <exception cref="InvalidOperationException">Thrown if advancing exceeds the buffer capacity.</exception>
    public void Advance(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        ObjectDisposedException.ThrowIf(_buffer is null, this);

        if (_index + count > _buffer.Length)
        {
            throw new InvalidOperationException("Cannot advance past the buffer capacity.");
        }

        _index += count;
    }

    /// <summary>
    /// Gets a <see cref="Memory{T}"/> block to write to.
    /// </summary>
    /// <param name="sizeHint">The minimum size of the requested memory block.</param>
    /// <returns>A memory block for writing.</returns>
    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        this.EnsureCapacity(sizeHint);
        return _buffer.AsMemory(_index);
    }

    /// <summary>
    /// Gets a <see cref="Span{T}"/> block to write to.
    /// </summary>
    /// <param name="sizeHint">The minimum size of the requested span block.</param>
    /// <returns>A span block for writing.</returns>
    public Span<byte> GetSpan(int sizeHint = 0)
    {
        this.EnsureCapacity(sizeHint);
        return _buffer.AsSpan(_index);
    }

    /// <summary>
    /// Extracts the written data into a <see cref="BufferLease"/>, transferring ownership of the rented buffer.
    /// </summary>
    /// <returns>A <see cref="BufferLease"/> wrapping the written bytes.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the writer has been disposed.</exception>
    public BufferLease ExtractLease()
    {
        ObjectDisposedException.ThrowIf(_buffer is null, this);

        byte[] buf = _buffer;
        int length = _index;

        _buffer = null;
        _index = 0;

        return BufferLease.TakeOwnership(buf, start: 0, length: length);
    }

    /// <summary>
    /// Disposes the buffer writer and returns the rented buffer to the pool if it hasn't been extracted.
    /// </summary>
    public void Dispose()
    {
        if (_buffer is not null)
        {
            BufferLease.ByteArrayPool.Return(_buffer);
            _buffer = null;
        }
        _index = 0;
    }

    private void EnsureCapacity(int sizeHint)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);

        ObjectDisposedException.ThrowIf(_buffer is null, this);

        if (sizeHint == 0)
        {
            sizeHint = 1;
        }

        int required = _index + sizeHint;
        if (required > _buffer.Length)
        {
            int newSize = Math.Max(_buffer.Length * 2, required);
            byte[] newBuffer = BufferLease.ByteArrayPool.Rent(newSize);
            _buffer.AsSpan(0, _index).CopyTo(newBuffer);
            BufferLease.ByteArrayPool.Return(_buffer);
            _buffer = newBuffer;
        }
    }
}
