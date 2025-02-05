namespace Notio.Network.Package.Enums;

/// <summary>
/// Defines the available compression modes for packets.
/// </summary>
public enum PacketCompressionMode : byte
{
    None = 0x00, // Không nén
    GZip = 0x01, // Nén bằng GZip
    Deflate = 0x02, // Nén bằng Deflate
    Brotli = 0x03  // Nén bằng Brotli
}