using Nalix.Shared.LZ4.Internal;

namespace Nalix.Shared.LZ4.Encoders;

/// <summary>
/// Provides methods to write literal bytes directly to a destination.
/// This class is designed for high-performance memory operations with minimal overhead.
/// </summary>
internal static unsafe class RawWriter
{
    /// <summary>
    /// Writes a sequence of literal bytes from a memory pointer to the destination.
    /// </summary>
    /// <param name="destPtr">The pointer to the destination memory location where the data will be written.</param>
    /// <param name="literalStartPtr">The pointer to the start of the literal data to be written.</param>
    /// <param name="length">The number of bytes to write from the literal data.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Write(ref byte* destPtr, byte* literalStartPtr, int length)
    {
        if (length <= 0) return;

        MemOps.Copy(literalStartPtr, destPtr, length);
        destPtr += length;
    }

    /// <summary>
    /// Writes a sequence of literal bytes from a span to the destination.
    /// </summary>
    /// <param name="destPtr">The pointer to the destination memory location where the data will be written.</param>
    /// <param name="literals">The span of bytes containing the literal data to be written.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Write(ref byte* destPtr, System.ReadOnlySpan<byte> literals)
    {
        if (literals.IsEmpty) return;

        MemOps.Copy(literals, destPtr);
        destPtr += literals.Length;
    }
}
