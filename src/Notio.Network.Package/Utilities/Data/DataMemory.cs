using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace Notio.Network.Package.Utilities.Data;

/// <summary>
/// Provides memory allocation utilities to optimize memory usage for different packet sizes.
/// This class determines the appropriate memory allocation strategy (stack, heap, or pool) based on the size of the payload.
/// </summary>
public static class DataMemory
{
    /// <summary>
    /// Allocates memory for the given payload based on its size, choosing between stack, heap, or pooled memory.
    /// </summary>
    /// <param name="payload">The memory to be allocated and copied into the new memory block.</param>
    /// <returns>A tuple containing the allocated memory and a flag indicating whether the memory is pooled.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Memory<byte> Allocate(Memory<byte> payload)
    {
        int length = payload.Length;

        if (length <= PacketConstants.MaxStackAllocSize)
        {
            Span<byte> stackBuffer = stackalloc byte[length];
            payload.Span.CopyTo(stackBuffer);
            return stackBuffer.ToArray();
        }
        else if (length <= PacketConstants.MaxHeapAllocSize)
        {
            byte[] buffer = GC.AllocateUninitializedArray<byte>(length, pinned: true);
            payload.Span.CopyTo(buffer);
            return buffer;
        }
        else
        {
            byte[] result = ArrayPool<byte>.Shared.Rent(length);
            payload.Span.CopyTo(result);
            return result;
        }
    }
}
