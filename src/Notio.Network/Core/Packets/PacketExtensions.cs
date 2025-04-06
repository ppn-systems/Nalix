using Notio.Common.Connection;
using Notio.Common.Package;

namespace Notio.Network.Core.Packets;

/// <summary>
/// Provides extension methods for packet operations.
/// </summary>
internal static class PacketExtensions
{
    /// <summary>
    /// Sends a string packet to the client.
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="code"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    internal static bool SendString(this IConnection connection, PacketCode code, string message)
      => connection.SendAsync(PacketBuilder.String(code, message)).Result;

    /// <summary>
    /// Sends a string packet to the client.
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="code"></param>
    /// <returns></returns>
    internal static bool SendString(this IConnection connection, PacketCode code)
      => connection.SendAsync(PacketBuilder.String(code)).Result;
}
