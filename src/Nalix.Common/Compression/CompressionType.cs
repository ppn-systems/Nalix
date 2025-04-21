namespace Nalix.Common.Compression;

/// <summary>
/// Represents the available compression types.
/// </summary>
public enum CompressionType : byte
{
    /// <summary>
    /// Represents GZip compression.
    /// </summary>
    GZip,

    /// <summary>Brotli compression.</summary>
    Brotli,

    /// <summary>Deflate compression.</summary>
    Deflate
}
