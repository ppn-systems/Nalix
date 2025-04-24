using System.Runtime.InteropServices;

namespace Nalix.Shared.LZ4.Internal;

/// <summary>
/// Defines the block header structure.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly struct Header(int originalLength, int compressedLength)
{
    public const int Size = sizeof(int) * 2; // 8 bytes

    public readonly int OriginalLength = originalLength;
    public readonly int CompressedLength = compressedLength; // Includes header size
}
