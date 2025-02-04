namespace Notio.Network.Package.Enums;

/// <summary>
/// Defines the available compression modes for packets.
/// </summary>
public enum PacketCompressionMode
{
    GZip,
    Deflate,
    Brotli
}