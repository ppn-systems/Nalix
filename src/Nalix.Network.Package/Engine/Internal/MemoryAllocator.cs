using Nalix.Common.Constants;

namespace Nalix.Network.Package.Engine.Internal;

/// <summary>
/// Provides memory allocation utilities to optimize memory usage for different packet sizes.
/// This class determines the appropriate memory allocation strategy (stack, heap, or pool) based on the size of the payload.
/// </summary>
internal static class MemoryAllocator
{
    /// <summary>
    /// Allocates memory for the given payload based on its size, choosing between stack, heap, or pooled memory.
    /// </summary>
    /// <param name="payload">The memory to be allocated and copied into the new memory block.</param>
    /// <returns>A tuple containing the allocated memory and a flag indicating whether the memory is pooled.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static System.ReadOnlyMemory<System.Byte> Allocate(System.ReadOnlyMemory<System.Byte> payload)
        => Allocate(payload.Span);

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static unsafe System.ReadOnlyMemory<System.Byte> Allocate(
        System.ReadOnlySpan<System.Byte> payload)
    {
        switch (payload.Length)
        {
            case 0:
                {
                    // For empty payloads, return an empty memory block
                    return System.Memory<System.Byte>.Empty;
                }
            case <= PacketConstants.StackAllocLimit:
                {
                    // Stack allocation remains the same - already optimal
                    System.Span<System.Byte> stack = stackalloc System.Byte[payload.Length];
                    payload.CopyTo(stack);
                    return stack.ToArray();
                }
            case <= PacketConstants.HeapAllocLimit:
                {
                    // Use unsafe for medium-sized payloads
                    System.Byte[] buffer = System.GC.AllocateUninitializedArray<System.Byte>(payload.Length, pinned: true);
                    fixed (System.Byte* destination = buffer)
                    fixed (System.Byte* source = payload)
                    {
                        System.Buffer.MemoryCopy(source, destination, payload.Length, payload.Length);
                    }
                    return buffer;
                }
            default:
                {
                    // Pool allocation with unsafe copy for large payloads
                    System.Byte[] rent = System.Buffers.ArrayPool<System.Byte>.Shared.Rent(payload.Length);
                    fixed (System.Byte* destination = rent)
                    fixed (System.Byte* source = payload)
                    {
                        System.Buffer.MemoryCopy(source, destination, payload.Length, payload.Length);
                    }
                    return rent;
                }
        }
    }
}