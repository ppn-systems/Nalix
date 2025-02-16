using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace Notio.Network.Package.Utilities;

internal static class DataMemory
{
    internal const int StackAllocThreshold = 256;
    internal const int HeapAllocThreshold = 1024;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static (Memory<byte> payload, bool isPooled) Allocate(Memory<byte> payload)
    {
        if (payload.IsEmpty) return (Memory<byte>.Empty, false);

        int length = payload.Length;

        if (length <= StackAllocThreshold)
        {
            Span<byte> stackBuffer = stackalloc byte[length];
            payload.Span.CopyTo(stackBuffer);
            return (stackBuffer.ToArray(), false);
        }
        else if (length <= HeapAllocThreshold)
        {
            byte[] buffer = GC.AllocateUninitializedArray<byte>(length, pinned: true);
            payload.Span.CopyTo(buffer);
            return (buffer, false);
        }
        else
        {
            byte[] result = ArrayPool<byte>.Shared.Rent(length);
            payload.Span.CopyTo(result);
            return (result, true);
        }
    }
}
