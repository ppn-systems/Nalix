using Nalix.Common.Constants;

namespace Nalix.Network.Package.Engine.Internal;

/// <summary>
/// Provides memory allocation utilities to optimize memory usage for different packet sizes.
/// This class determines the appropriate memory allocation strategy (stack, heap, or pool) based on the size of the payload.
/// </summary>
public static class MemoryAllocator
{
    /// <summary>
    /// Allocates memory for the given payload based on its size, choosing between stack, heap, or pooled memory.
    /// </summary>
    /// <param name="payload">The memory to be allocated and copied into the new memory block.</param>
    /// <returns>A tuple containing the allocated memory and a flag indicating whether the memory is pooled.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Memory<byte> Allocate(System.Memory<byte> payload)
    {
        int length = payload.Length;

        switch (length)
        {
            case <= PacketConstants.StackAllocLimit:
                {
                    System.Span<byte> stackBuffer = stackalloc byte[length];
                    payload.Span.CopyTo(stackBuffer);
                    return stackBuffer.ToArray();
                }
            case <= PacketConstants.HeapAllocLimit:
                {
                    byte[] buffer = System.GC.AllocateUninitializedArray<byte>(length, pinned: true);
                    payload.Span.CopyTo(buffer);
                    return buffer;
                }
            default:
                {
                    byte[] result = System.Buffers.ArrayPool<byte>.Shared.Rent(length);
                    payload.Span.CopyTo(result);
                    return result;
                }
        }
    }
}
