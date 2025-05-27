using Nalix.Common.Attributes;
using Nalix.Common.Connection;
using Nalix.Common.Connection.Protocols;
using Nalix.Common.Constants;
using Nalix.Common.Package;
using Nalix.Common.Package.Enums;
using Nalix.Common.Security;
using Nalix.Environment;
using Nalix.Serialization;
using Nalix.Shared.Contracts;
using System.Runtime.CompilerServices;

namespace Nalix.Shared.Net.Operations;

/// <summary>
/// A controller for managing keep-alive packets in a network dispatcher.
/// </summary>
[PacketController]
internal sealed class KeepAliveOps<TPacket> where TPacket : IPacket, IPacketFactory<TPacket>
{
    /// <summary>
    /// Handles a ping request from the client.
    /// </summary>
    [PacketEncryption(false)]
    [PacketTimeout(Timeouts.Short)]
    [PacketOpcode((ushort)ProtocolCommand.Ping)]
    [PacketPermission(PermissionLevel.Guest)]
    [PacketRateLimit(RequestLimitType.Low)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static System.Memory<byte> Ping(TPacket _, IConnection __)
        => TPacket.Create(
            (ushort)ProtocolCommand.Pong, PacketType.String, PacketFlags.None,
            PacketPriority.Low, SerializationOptions.Encoding.GetBytes("Pong")).Serialize();

    /// <summary>
    /// Handles a ping request from the client.
    /// </summary>
    [PacketEncryption(false)]
    [PacketTimeout(Timeouts.Short)]
    [PacketPermission(PermissionLevel.Guest)]
    [PacketOpcode((ushort)ProtocolCommand.Pong)]
    [PacketRateLimit(RequestLimitType.Low)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static System.Memory<byte> Pong(TPacket _, IConnection __)
        => TPacket.Create(
            (ushort)ProtocolCommand.Ping, PacketType.String, PacketFlags.None,
            PacketPriority.Low, SerializationOptions.Encoding.GetBytes("Ping")).Serialize();

    /// <summary>
    /// Returns the round-trip time (RTT) of the connection in milliseconds.
    /// </summary>
    [PacketEncryption(false)]
    [PacketTimeout(Timeouts.Short)]
    [PacketPermission(PermissionLevel.Guest)]
    [PacketOpcode((ushort)ProtocolCommand.PingTime)]
    [PacketRateLimit(RequestLimitType.Low)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static System.Memory<byte> GetPingTime(TPacket _, IConnection connection)
        => TPacket.Create(
            (ushort)ProtocolCommand.PingTime, PacketType.String, PacketFlags.None,
            PacketPriority.Low, SerializationOptions.Encoding.GetBytes($"Ping: {connection.LastPingTime} ms")).Serialize();

    /// <summary>
    /// Returns the ping information of the connection, including up time and last ping time.
    /// </summary>
    [PacketEncryption(false)]
    [PacketTimeout(Timeouts.Short)]
    [PacketPermission(PermissionLevel.Guest)]
    [PacketOpcode((ushort)ProtocolCommand.PingInfo)]
    [PacketRateLimit(RequestLimitType.Low)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static System.Memory<byte> GetPingInfo(TPacket _, IConnection connection)
    {
        PingInfoDto pingInfoDto = new()
        {
            UpTime = connection.UpTime,
            LastPingTime = connection.LastPingTime,
        };

        byte[] data = BitSerializer.Serialize(pingInfoDto);
        return System.MemoryExtensions.AsMemory(data);
    }
}
