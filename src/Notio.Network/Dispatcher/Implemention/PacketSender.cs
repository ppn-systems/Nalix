using Notio.Common.Connection;
using Notio.Common.Package;
using Notio.Common.Package.Metadata;
using Notio.Defaults;
using Notio.Utilities;
using System;

namespace Notio.Network.Dispatcher.Implemention;

internal static class PacketSender
{
    /// <summary>
    /// Creates and sends an error packet with a string message to the client.
    /// </summary>
    /// <param name="connection">The connection to send the packet through.</param>
    /// <param name="message">The message to send.</param>
    /// <param name="command">The command identifier.</param>
    /// <returns>True if the packet was sent successfully; otherwise, false.</returns>
    public static bool StringPacket(IConnection connection, string message, short command)
        => SendPacket(connection, DefaultConstants.DefaultEncoding.GetBytes(message), PacketType.String, command);

    /// <summary>
    /// Creates and sends a binary packet containing the server's public key to the client.
    /// </summary>
    /// <param name="connection">The connection to send the packet through.</param>
    /// <param name="payload">The payload to send.</param>
    /// <param name="command">The command identifier.</param>
    /// <returns>True if the packet was sent successfully; otherwise, false.</returns>
    public static bool BinaryPacket(IConnection connection, byte[] payload, short command)
        => SendPacket(connection, payload, PacketType.Binary, command);

    /// <summary>
    /// Common method for creating and sending packets.
    /// </summary>
    /// <param name="connection">The connection to send the packet through.</param>
    /// <param name="payload">The payload to send.</param>
    /// <param name="packetType">The type of the packet.</param>
    /// <param name="command">The command identifier.</param>
    /// <returns>True if the packet was sent successfully; otherwise, false.</returns>
    private static bool SendPacket(IConnection connection, byte[] payload, PacketType packetType, short command)
    {
        ulong timestamp = MicrosecondClock.GetTimestamp();
        ushort totalLength = (ushort)(PacketSize.Header + payload.Length);
        byte[] packet = new byte[totalLength];

        // Populate the header
        Array.Copy(BitConverter.GetBytes(totalLength), 0, packet, PacketOffset.Length, PacketSize.Length);
        Array.Copy(BitConverter.GetBytes((ushort)0), 0, packet, PacketOffset.Id, PacketSize.Id);

        packet[PacketOffset.Type] = (byte)packetType;
        packet[PacketOffset.Flags] = (byte)PacketFlags.None;
        packet[PacketOffset.Priority] = (byte)PacketPriority.None;

        Array.Copy(BitConverter.GetBytes(command), 0, packet, PacketOffset.Command, PacketSize.Command);
        Array.Copy(BitConverter.GetBytes(timestamp), 0, packet, PacketOffset.Timestamp, PacketSize.Timestamp);

        // Populate the payload
        Array.Copy(payload, 0, packet, PacketOffset.Payload, payload.Length);

        // Calculate and set the checksum
        uint checksum = CalculateChecksum(packet);
        Array.Copy(BitConverter.GetBytes(checksum), 0, packet, PacketOffset.Checksum, PacketSize.Checksum);

        // Send the packet to the client
        return connection.Send(packet);
    }

    /// <summary>
    /// Calculates the CRC32 checksum for the given data.
    /// </summary>
    /// <param name="data">The data to calculate the checksum for.</param>
    /// <returns>The calculated CRC32 checksum.</returns>
    private static uint CalculateChecksum(byte[] data)
    {
        // Simple CRC32 implementation; consider replacing with a library for production use
        uint crc = 0xFFFFFFFF;
        foreach (byte b in data)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
            {
                crc = (uint)((crc >> 1) ^ (0xEDB88320 & -(crc & 1)));
            }
        }
        return ~crc;
    }
}
