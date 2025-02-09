namespace Notio.Network.Package.Enums;

/// <summary>
/// Defines the available compression modes for packets.
/// </summary>
public enum PacketCompressionMode : byte
{
    /// <summary>
    /// No compression applied to the packet.
    /// </summary>
    None = 0x00, // Không nén

    /// <summary>
    /// Compression using the GZip algorithm.
    /// </summary>
    GZip = 0x01, // Nén bằng GZip

    /// <summary>
    /// Compression using the Deflate algorithm.
    /// </summary>
    Deflate = 0x02, // Nén bằng Deflate

    /// <summary>
    /// Compression using the Brotli algorithm.
    /// </summary>
    Brotli = 0x03  // Nén bằng Brotli
}