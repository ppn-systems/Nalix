namespace Nalix.Serialization.Internal.Writers;

internal struct BufferSegment(int size)
{
    private int _written = 0;
    private byte[] _buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(size);

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
