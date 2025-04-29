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
        => Allocate(payload.Span);

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static unsafe System.Memory<byte> Allocate(System.ReadOnlySpan<byte> payload)
    {
        switch (payload.Length)
        {
            case 0:
                {
                    // For empty payloads, return an empty memory block
                    return System.Memory<byte>.Empty;
                }
            case <= PacketConstants.StackAllocLimit:
                {
                    // Stack allocation remains the same - already optimal
                    System.Span<byte> stack = stackalloc byte[payload.Length];
                    payload.CopyTo(stack);
                    return stack.ToArray();
                }
            case <= PacketConstants.HeapAllocLimit:
                {
                    // Use unsafe for medium-sized payloads
                    byte[] buffer = System.GC.AllocateUninitializedArray<byte>(payload.Length, pinned: true);
                    fixed (byte* destination = buffer)
                    fixed (byte* source = payload)
                    {
                        System.Buffer.MemoryCopy(source, destination, payload.Length, payload.Length);
                    }
                    return buffer;
                }
            default:
                {
                    // Pool allocation with unsafe copy for large payloads
                    byte[] rent = PacketConstants.Pool.Rent(payload.Length);
                    fixed (byte* destination = rent)
                    fixed (byte* source = payload)
                    {
                        System.Buffer.MemoryCopy(source, destination, payload.Length, payload.Length);
                    }
                    return rent;
                }
        }
    }
}
