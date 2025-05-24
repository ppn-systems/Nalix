using Nalix.Common.Exceptions;

namespace Nalix.Serialization.Internal.Writers;

internal struct BufferWriterFixed(byte[] buffer) : System.Buffers.IBufferWriter<byte>
{
    private readonly byte[] buffer = buffer;
    private int written = 0;

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Advance(int count) => written += count;

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public readonly System.Memory<byte> GetMemory(int sizeHint = 0)
    {
        System.Memory<byte> memory = System.MemoryExtensions.AsMemory(buffer, written);
        if (memory.Length >= sizeHint)
        {
            return memory;
        }

        throw new SerializationException("Requested invalid sizeHint.");
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public readonly System.Span<byte> GetSpan(int sizeHint = 0)
    {
        System.Span<byte> span = System.MemoryExtensions.AsSpan(buffer, written);
        if (span.Length >= sizeHint)
        {
            return span;
        }

        throw new SerializationException("Requested invalid sizeHint.");
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public readonly byte[] GetFilledBuffer()
    {
        if (written != buffer.Length)
        {
            throw new SerializationException("Not filled buffer.");
        }

        return buffer;
    }
}
