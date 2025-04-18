using Notio.Common.Connection;
using Notio.Common.Constants;
using Notio.Common.Package;
using Notio.Common.Package.Attributes;
using Notio.Common.Package.Enums;
using Notio.Common.Security;
using Notio.Network.Dispatch.Core.Dto;
using Notio.Network.Dispatch.Core.Packets;
using Notio.Network.Dispatch.Dto;

namespace Notio.Network.Dispatch.BuiltIn;

/// <summary>
/// A controller for managing keep-alive packets in a network dispatcher.
/// </summary>
[PacketController]
public sealed class KeepAliveController
{
    /// <summary>
    /// Handles a ping request from the client.
    /// </summary>
    [PacketEncryption(false)]
    [PacketTimeout(Timeouts.Short)]
    [PacketPermission(PermissionLevel.Guest)]
    [PacketRateGroup(nameof(KeepAliveController))]
    [PacketId((ushort)ProtocolPacket.Ping)]
    [PacketRateLimit(MaxRequests = 10, LockoutDurationSeconds = 1000)]
    public static System.Memory<byte> Ping(IPacket _, IConnection __)
        => PacketBuilder.String(PacketCode.Success, "Pong");

    /// <summary>
    /// Handles a ping request from the client.
    /// </summary>
    [PacketEncryption(false)]
    [PacketTimeout(Timeouts.Short)]
    [PacketPermission(PermissionLevel.Guest)]
    [PacketRateGroup(nameof(KeepAliveController))]
    [PacketId((ushort)ProtocolPacket.Pong)]
    [PacketRateLimit(MaxRequests = 10, LockoutDurationSeconds = 1000)]
    public static System.Memory<byte> Pong(IPacket _, IConnection __)
        => PacketBuilder.String(PacketCode.Success, "Ping");

    /// <summary>
    /// Returns the round-trip time (RTT) of the connection in milliseconds.
    /// </summary>
    [PacketEncryption(false)]
    [PacketTimeout(Timeouts.Short)]
    [PacketPermission(PermissionLevel.Guest)]
    [PacketRateGroup(nameof(SessionController))]
    [PacketId((ushort)ProtocolPacket.PingTime)]
    [PacketRateLimit(MaxRequests = 2, LockoutDurationSeconds = 20)]
    public static System.Memory<byte> GetPingTime(IPacket _, IConnection connection)
        => PacketBuilder.String(PacketCode.Success, $"Ping: {connection.LastPingTime} ms");

    /// <summary>
    /// Returns the ping information of the connection, including up time and last ping time.
    /// </summary>
    [PacketEncryption(false)]
    [PacketTimeout(Timeouts.Short)]
    [PacketPermission(PermissionLevel.Guest)]
    [PacketRateGroup(nameof(SessionController))]
    [PacketId((ushort)ProtocolPacket.PingInfo)]
    [PacketRateLimit(MaxRequests = 2, LockoutDurationSeconds = 20)]
    public static System.Memory<byte> GetPingInfo(IPacket _, IConnection connection)
    {
        PingInfoDto pingInfoDto = new()
        {
            UpTime = connection.UpTime,
            LastPingTime = connection.LastPingTime,
        };

        return PacketBuilder.Json(PacketCode.Success, pingInfoDto, NotioJsonContext.Default.PingInfoDto);
    }
}
