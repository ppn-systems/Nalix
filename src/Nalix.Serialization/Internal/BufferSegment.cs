namespace Nalix.Serialization.Internal;

[System.Runtime.InteropServices.StructLayout(
    System.Runtime.InteropServices.LayoutKind.Auto)]
internal struct BufferSegment
{
    private int _read;
    private int _written;
    private byte[] _buffer;

    public BufferSegment(int size)
    {
        if (size <= 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(size), "Size must be greater than zero.");
        }

        _read = 0;
        _written = 0;
        _buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(size);
    }

    public readonly int ReadCount => _read;
    public readonly int WrittenCount => _written;
    public readonly bool IsNull => _buffer == null;

    public readonly System.Span<byte> FreeBuffer => System.MemoryExtensions.AsSpan(_buffer, _written);
    public readonly System.Span<byte> WrittenBuffer => System.MemoryExtensions.AsSpan(_buffer, 0, _written);
    public readonly System.Memory<byte> WrittenMemory => System.MemoryExtensions.AsMemory(_buffer, 0, _written);
    public readonly System.ReadOnlySpan<byte> ReadBuffer => System.MemoryExtensions.AsSpan(_buffer, _read, _written - _read);

    [System.Runtime.CompilerServices.MethodImpl(
    System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void AdvanceRead(int count) => _read += count;

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void AdvanceWrite(int count) => _written += count;

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        if (_buffer != null)
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(_buffer);
        }

        _read = 0;
        _written = 0;
        _buffer = null;
    }
}
