using Notio.Common.Connection;
using Notio.Common.Constants;
using Notio.Common.Package;
using Notio.Common.Package.Attributes;
using Notio.Common.Package.Enums;
using Notio.Common.Security;
using Notio.Network.Core;
using Notio.Network.Core.Packets;
using System;

namespace Notio.Network.Dispatcher.BuiltIn;

/// <summary>
/// A controller for managing keep-alive packets in a network dispatcher.
/// </summary>
[PacketController]
public static class KeepAliveController
{
    /// <summary>
    /// Handles a ping request from the client.
    /// </summary>
    [PacketEncryption(false)]
    [PacketTimeout(Timeouts.Short)]
    [PacketPermission(PermissionLevel.Guest)]
    [PacketRateGroup(nameof(KeepAliveController))]
    [PacketId((ushort)InternalProtocolCommand.Ping)]
    [PacketRateLimit(MaxRequests = 10, LockoutDurationSeconds = 1000)]
    public static Memory<byte> Ping(IPacket _, IConnection __)
        => PacketBuilder.String(PacketCode.Success, "Pong");

    /// <summary>
    /// Handles a ping request from the client.
    /// </summary>
    [PacketEncryption(false)]
    [PacketTimeout(Timeouts.Short)]
    [PacketPermission(PermissionLevel.Guest)]
    [PacketRateGroup(nameof(KeepAliveController))]
    [PacketId((ushort)InternalProtocolCommand.Ping)]
    [PacketRateLimit(MaxRequests = 10, LockoutDurationSeconds = 1000)]
    public static Memory<byte> Pong(IPacket _, IConnection __)
        => PacketBuilder.String(PacketCode.Success, "Ping");
}
