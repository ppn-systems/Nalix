using Notio.Common.Connection;
using Notio.Common.Package;
using Notio.Defaults;

namespace Notio.Network.Core.Packets;

internal static class PacketSender
{
    internal static bool SendBinary(IConnection connection, PacketCode code, byte[] payload)
        => SendPacket(connection, code, PacketType.Binary, payload);

    internal static bool SendString(IConnection connection, PacketCode code, string message)
        => SendPacket(connection, code, PacketType.String, DefaultConstants.DefaultEncoding.GetBytes(message));

    /// <summary>
    /// Sends a raw packet to the client.
    /// </summary>
    /// <param name="connection">Target connection.</param>
    /// <param name="code">Packet code.</param>
    /// <param name="type">Packet type.</param>
    /// <param name="payload">Raw payload.</param>
    internal static bool SendPacket(IConnection connection, PacketCode code, PacketType type, byte[] payload)
        => connection.Send(PacketBuilder.Assemble(code, type, payload));
}
