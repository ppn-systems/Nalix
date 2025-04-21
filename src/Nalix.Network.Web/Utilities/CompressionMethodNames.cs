namespace Nalix.Network.Web.Utilities;

/// <summary>
/// Exposes constants for possible values of the <c>Content-DefaultEncodings</c> HTTP header.
/// </summary>
/// <see cref="Enums.CompressionMethod"/>
public static class CompressionMethodNames
{
    /// <summary>
    /// Specifies no compression.
    /// </summary>
    /// <see cref="Enums.CompressionMethod.None"/>
    public const string None = "identity";

    /// <summary>
    /// Specifies the "Deflate" compression method.
    /// </summary>
    /// <see cref="Enums.CompressionMethod.Deflate"/>
    public const string Deflate = "deflate";

    /// <summary>
    /// Specifies the GZip compression method.
    /// </summary>
    /// <see cref="Enums.CompressionMethod.Gzip"/>
    public const string Gzip = "gzip";
}
