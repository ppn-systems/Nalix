using Nalix.Common.Connection;
using Nalix.Common.Connection.Contracts;
using Nalix.Common.Constants;
using Nalix.Common.Package;
using Nalix.Common.Package.Attributes;
using Nalix.Common.Package.Enums;
using Nalix.Common.Security;
using Nalix.Serialization;
using System.Runtime.CompilerServices;

namespace Nalix.Shared.Net.Operations;

/// <summary>
/// A controller for managing keep-alive packets in a network dispatcher.
/// </summary>
[PacketController]
public sealed class KeepAliveOps<TPacket> where TPacket : IPacket, IPacketFactory<TPacket>
{
    /// <summary>
    /// Handles a ping request from the client.
    /// </summary>
    [PacketEncryption(false)]
    [PacketTimeout(Timeouts.Short)]
    [PacketId((ushort)ConnectionCommand.Ping)]
    [PacketPermission(PermissionLevel.Guest)]
    [PacketRateGroup(nameof(KeepAliveOps<TPacket>))]
    [PacketRateLimit(MaxRequests = 10, LockoutDurationSeconds = 1000)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static System.Memory<byte> Ping(TPacket _, IConnection __)
        => TPacket.Create(
            (ushort)ConnectionCommand.Pong, PacketCode.Success, PacketType.String,
            PacketFlags.None, PacketPriority.Low, JsonOptions.Encoding.GetBytes("Pong")).Serialize();

    /// <summary>
    /// Handles a ping request from the client.
    /// </summary>
    [PacketEncryption(false)]
    [PacketTimeout(Timeouts.Short)]
    [PacketPermission(PermissionLevel.Guest)]
    [PacketId((ushort)ConnectionCommand.Pong)]
    [PacketRateGroup(nameof(KeepAliveOps<TPacket>))]
    [PacketRateLimit(MaxRequests = 10, LockoutDurationSeconds = 1000)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static System.Memory<byte> Pong(TPacket _, IConnection __)
        => TPacket.Create(
            (ushort)ConnectionCommand.Ping, PacketCode.Success, PacketType.String,
            PacketFlags.None, PacketPriority.Low, JsonOptions.Encoding.GetBytes("Ping")).Serialize();

    /// <summary>
    /// Returns the round-trip time (RTT) of the connection in milliseconds.
    /// </summary>
    [PacketEncryption(false)]
    [PacketTimeout(Timeouts.Short)]
    [PacketPermission(PermissionLevel.Guest)]
    [PacketId((ushort)ConnectionCommand.PingTime)]
    [PacketRateGroup(nameof(ConnectionOps<TPacket>))]
    [PacketRateLimit(MaxRequests = 2, LockoutDurationSeconds = 20)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static System.Memory<byte> GetPingTime(TPacket _, IConnection connection)
        => TPacket.Create(
            (ushort)ConnectionCommand.PingTime, PacketCode.Success, PacketType.String, PacketFlags.None,
            PacketPriority.Low, JsonOptions.Encoding.GetBytes($"Ping: {connection.LastPingTime} ms")).Serialize();

    /// <summary>
    /// Returns the ping information of the connection, including up time and last ping time.
    /// </summary>
    [PacketEncryption(false)]
    [PacketTimeout(Timeouts.Short)]
    [PacketPermission(PermissionLevel.Guest)]
    [PacketId((ushort)ConnectionCommand.PingInfo)]
    [PacketRateGroup(nameof(ConnectionOps<TPacket>))]
    [PacketRateLimit(MaxRequests = 2, LockoutDurationSeconds = 20)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static System.Memory<byte> GetPingInfo(TPacket _, IConnection connection)
    {
        PingInfoDto pingInfoDto = new()
        {
            UpTime = connection.UpTime,
            LastPingTime = connection.LastPingTime,
        };

        return TPacket.Create(
            (ushort)ConnectionCommand.PingInfo, PacketCode.Success, PacketType.String, PacketFlags.None,
            PacketPriority.Low, JsonCodec.SerializeToMemory(pingInfoDto, NetJsonCxt.Default.PingInfoDto)).Serialize();
    }
}
