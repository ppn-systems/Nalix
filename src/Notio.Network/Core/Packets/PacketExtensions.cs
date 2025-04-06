using Notio.Common.Connection;
using Notio.Common.Package;

namespace Notio.Network.Core.Packets;

/// <summary>
/// Provides extension methods for packet operations.
/// </summary>
internal static class PacketExtensions
{
    /// <summary>
    /// Sends a binary packet to the client.
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="id"></param>
    /// <param name="code"></param>
    /// <param name="payload"></param>
    /// <returns></returns>
    internal static bool SendBinary(this IConnection connection, short id, PacketCode code, byte[] payload)
        => PacketSender.SendBinary(connection, id, code, payload);

    /// <summary>
    /// Sends a string packet to the client.
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="id"></param>
    /// <param name="code"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    internal static bool SendString(this IConnection connection, short id, PacketCode code, string message)
       => PacketSender.SendString(connection, id, code, message);
}
