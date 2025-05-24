namespace Nalix.Serialization.Internal.Writers;

internal struct BufferSegment
{
    private int _written;
    private byte[] _buffer;

    public BufferSegment(int size)
    {
        if (size <= 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(size), "Size must be greater than zero.");
        }
        _buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(size);
        _written = 0;
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
        if (_buffer != null)
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(_buffer);
        }

        _buffer = null!;
        _written = 0;
    }
}
