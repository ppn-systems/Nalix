namespace Nalix.Serialization.Internal;

[System.Runtime.InteropServices.StructLayout(
    System.Runtime.InteropServices.LayoutKind.Auto)]
internal struct BufferSegment
{
    private readonly bool _rent;

    private int _written;
    private byte[] _buffer;

    public BufferSegment(int size)
    {
        if (size <= 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(size), "Size must be greater than zero.");
        }

        _rent = true;
        _written = 0;
        _buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(size);
    }

    public BufferSegment(byte[] buffer)
    {
        if (buffer.Length <= 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(buffer), "Size must be greater than zero.");
        }

        _rent = false;
        _written = 0;
        _buffer = buffer;
    }

    public readonly int WrittenCount => _written;
    public readonly bool IsNull => _buffer == null;
    public readonly int Length => _buffer?.Length ?? 0;

    public readonly System.Span<byte> FreeBuffer => System.MemoryExtensions.AsSpan(_buffer, _written);
    public readonly System.Span<byte> WrittenBuffer => System.MemoryExtensions.AsSpan(_buffer, 0, _written);
    public readonly System.Memory<byte> WrittenMemory => System.MemoryExtensions.AsMemory(_buffer, 0, _written);

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Advance(int count) => _written += count;

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Expand(int minimumSize)
    {
        if (minimumSize <= 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(minimumSize), "Size must be greater than zero.");
        }

        if (_buffer != null && _buffer.Length - _written >= minimumSize)
        {
            return;
        }

        int newSize = System.Math.Max((_buffer?.Length ?? 0) * 2, _written + minimumSize);
        byte[] newBuffer = System.Buffers.ArrayPool<byte>.Shared.Rent(newSize);

        if (_buffer != null && _written > 0)
        {
            System.Buffer.BlockCopy(_buffer, 0, newBuffer, 0, _written);
        }

        if (_buffer != null && _rent)
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(_buffer);
        }

        _buffer = newBuffer;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        if (_buffer != null && _rent == true)
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(_buffer);
            _written = 0;
            _buffer = null;
        }
    }
}
