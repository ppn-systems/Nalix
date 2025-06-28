namespace Nalix.Shared.LZ4.Internal;

/// <summary>
/// Defines the block header structure used in Nalix compression, which contains metadata
/// about the original and compressed data lengths.
/// </summary>
/// <remarks>
/// The header is a fixed-size structure that precedes the compressed data. It contains
/// information necessary to properly decompress the data, such as the original data length
/// and the total compressed length (including the header size).
/// </remarks>
/// <param name="originalLength">The length of the original data before compression.</param>
/// <param name="compressedLength">The total length of the compressed data, including the header.</param>
[System.Runtime.InteropServices.StructLayout(
    System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)]
public readonly struct Header(System.Int32 originalLength, System.Int32 compressedLength)
{
    /// <summary>
    /// The size of the header structure in bytes. This is the sum of the sizes of the two integer fields.
    /// </summary>
    public const System.Int32 Size = sizeof(System.Int32) * 2; // 8 bytes

    /// <summary>
    /// Gets the original length of the data before compression.
    /// </summary>
    public readonly System.Int32 OriginalLength = originalLength;

    /// <summary>
    /// Gets the total length of the compressed data, including the size of the header.
    /// </summary>
    public readonly System.Int32 CompressedLength = compressedLength;
}
