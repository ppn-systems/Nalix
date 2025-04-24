using Nalix.Shared.LZ4.Internal;
using System.Runtime.CompilerServices;

namespace Nalix.Shared.LZ4.Encoders;

/// <summary>
/// Writes literal bytes directly.
/// </summary>
internal static unsafe class RawWriter
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(ref byte* destPtr, byte* literalStartPtr, int length)
    {
        if (length > 0)
        {
            MemOps.Copy(literalStartPtr, destPtr, length);
            destPtr += length;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(ref byte* destPtr, System.ReadOnlySpan<byte> literals)
    {
        if (!literals.IsEmpty)
        {
            MemOps.Copy(literals, destPtr);
            destPtr += literals.Length;
        }
    }
}
