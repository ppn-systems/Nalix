using Notio.Common.Connection;
using Notio.Common.Connection.Contracts;
using Notio.Common.Constants;
using Notio.Common.Package;
using Notio.Common.Package.Attributes;
using Notio.Common.Package.Enums;
using Notio.Common.Security;
using Notio.Network.Dispatch.BuiltIn.Internal;

namespace Notio.Network.Dispatch.BuiltIn;

/// <summary>
/// A controller for managing keep-alive packets in a network dispatcher.
/// </summary>
[PacketController]
public sealed class KeepAliveController<TPacket> where TPacket : IPacket
{
    /// <summary>
    /// Handles a ping request from the client.
    /// </summary>
    [PacketEncryption(false)]
    [PacketTimeout(Timeouts.Short)]
    [PacketId((ushort)ProtocolCommand.Ping)]
    [PacketPermission(PermissionLevel.Guest)]
    [PacketRateGroup(nameof(KeepAliveController<TPacket>))]
    [PacketRateLimit(MaxRequests = 10, LockoutDurationSeconds = 1000)]
    internal static System.Memory<byte> Ping(TPacket _, IConnection __)
        => PacketWriter.String(PacketCode.Success, "Pong");

    /// <summary>
    /// Handles a ping request from the client.
    /// </summary>
    [PacketEncryption(false)]
    [PacketTimeout(Timeouts.Short)]
    [PacketPermission(PermissionLevel.Guest)]
    [PacketId((ushort)ProtocolCommand.Pong)]
    [PacketRateGroup(nameof(KeepAliveController<TPacket>))]
    [PacketRateLimit(MaxRequests = 10, LockoutDurationSeconds = 1000)]
    internal static System.Memory<byte> Pong(TPacket _, IConnection __)
        => PacketWriter.String(PacketCode.Success, "Ping");

    /// <summary>
    /// Returns the round-trip time (RTT) of the connection in milliseconds.
    /// </summary>
    [PacketEncryption(false)]
    [PacketTimeout(Timeouts.Short)]
    [PacketPermission(PermissionLevel.Guest)]
    [PacketId((ushort)ProtocolCommand.PingTime)]
    [PacketRateGroup(nameof(SessionController<TPacket>))]
    [PacketRateLimit(MaxRequests = 2, LockoutDurationSeconds = 20)]
    internal static System.Memory<byte> GetPingTime(TPacket _, IConnection connection)
        => PacketWriter.String(PacketCode.Success, $"Ping: {connection.LastPingTime} ms");

    /// <summary>
    /// Returns the ping information of the connection, including up time and last ping time.
    /// </summary>
    [PacketEncryption(false)]
    [PacketTimeout(Timeouts.Short)]
    [PacketPermission(PermissionLevel.Guest)]
    [PacketId((ushort)ProtocolCommand.PingInfo)]
    [PacketRateGroup(nameof(SessionController<TPacket>))]
    [PacketRateLimit(MaxRequests = 2, LockoutDurationSeconds = 20)]
    internal static System.Memory<byte> GetPingInfo(TPacket _, IConnection connection)
    {
        PingInfoDto pingInfoDto = new()
        {
            UpTime = connection.UpTime,
            LastPingTime = connection.LastPingTime,
        };

        return PacketWriter.Json(PacketCode.Success, pingInfoDto, NetJsonCxt.Default.PingInfoDto);
    }
}
