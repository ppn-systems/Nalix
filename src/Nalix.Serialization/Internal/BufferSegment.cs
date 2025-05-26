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

    public readonly System.Span<byte> FreeBuffer => System.MemoryExtensions.AsSpan(_buffer, _written);
    public readonly System.Span<byte> WrittenBuffer => System.MemoryExtensions.AsSpan(_buffer, 0, _written);
    public readonly System.Memory<byte> WrittenMemory => System.MemoryExtensions.AsMemory(_buffer, 0, _written);

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Advance(int count) => _written += count;

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
