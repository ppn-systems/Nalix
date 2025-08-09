namespace Nalix.Shared.LZ4.Extensions;

/// <summary>
/// Provides string helpers to compress/decompress UTF-8 text using LZ4 and encode/decode as Base64.
/// </summary>
public static class StringCompressionExtensions
{
    /// <summary>
    /// Compresses the specified text using UTF-8 + LZ4 and returns a Base64-encoded string.
    /// </summary>
    /// <param name="text">The input text to compress. If null or empty, returns <see cref="System.String.Empty"/>.</param>
    /// <returns>
    /// A Base64 string that contains the LZ4-compressed representation of <paramref name="text"/>.
    /// Returns <see cref="System.String.Empty"/> when <paramref name="text"/> is null or empty.
    /// </returns>
    /// <remarks>
    /// This method allocates for the UTF-8 bytes, the compressed buffer, and the Base64 output string.
    /// For most application scenarios, this is sufficient and performant.
    /// </remarks>
    /// <example>
    /// <code>
    /// string packed = "hello".CompressToBase64();
    /// </code>
    /// </example>
    /// <exception cref="System.InvalidOperationException">Thrown when compression fails.</exception>
    public static System.String CompressToBase64(this System.String? text)
    {
        if (System.String.IsNullOrEmpty(text))
        {
            return System.String.Empty;
        }

        // UTF-8 -> LZ4 -> Base64
        System.Byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(text);
        System.Byte[] compressed = LZ4Codec.Encode(utf8);
        return System.Convert.ToBase64String(compressed);
    }

    /// <summary>
    /// Decodes a Base64 string that contains LZ4-compressed UTF-8 data and returns the original text.
    /// </summary>
    /// <param name="base64">
    /// The Base64 string to decode. If null or empty, returns <see cref="System.String.Empty"/>.
    /// </param>
    /// <returns>
    /// The decompressed UTF-8 text represented by <paramref name="base64"/>.
    /// Returns <see cref="System.String.Empty"/> when <paramref name="base64"/> is null or empty.
    /// </returns>
    /// <remarks>
    /// Internally performs Base64 decode, LZ4 decode, then converts bytes to UTF-8 text.
    /// </remarks>
    /// <example>
    /// <code>
    /// string text = packed.DecompressFromBase64();
    /// </code>
    /// </example>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when Base64 is invalid or decompression fails.
    /// </exception>
    public static System.String DecompressFromBase64(this System.String? base64)
    {
        if (System.String.IsNullOrEmpty(base64))
        {
            return System.String.Empty;
        }

        System.Byte[] compressed;
        try
        {
            compressed = System.Convert.FromBase64String(base64);
        }
        catch (System.FormatException ex)
        {
            throw new System.InvalidOperationException("Invalid Base64 input.", ex);
        }

        return !Nalix.Shared.LZ4.LZ4Codec.Decode(compressed, out System.Byte[]? output, out System.Int32 written) ||
            output is null || written <= 0
            ? throw new System.InvalidOperationException("LZ4 decompression failed.")
            : System.Text.Encoding.UTF8.GetString(output, 0, written);
    }
}
