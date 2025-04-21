using Nalix.Common.Connection;
using Nalix.Common.Connection.Contracts;
using Nalix.Common.Constants;
using Nalix.Common.Package;
using Nalix.Common.Package.Attributes;
using Nalix.Common.Package.Enums;
using Nalix.Common.Security;
using Nalix.Network.Dispatch.BuiltIn.Internal;

namespace Nalix.Network.Dispatch.BuiltIn;

/// <summary>
/// Provides handlers for managing connection-level configuration commands, 
/// such as setting compression and encryption modes during the handshake phase.
/// This controller is designed to be used with Dependency Injection and supports logging.
/// </summary>
[PacketController]
public sealed class SessionController<TPacket> where TPacket : IPacket
{
    /// <summary>
    /// Handles a client-initiated disconnect request.
    /// </summary>
    [PacketEncryption(false)]
    [PacketTimeout(Timeouts.Short)]
    [PacketPermission(PermissionLevel.Guest)]
    [PacketRateGroup(nameof(SessionController<TPacket>))]
    [PacketId((ushort)ProtocolCommand.Disconnect)]
    [PacketRateLimit(MaxRequests = 2, LockoutDurationSeconds = 20)]
    internal static void Disconnect(IPacket _, IConnection connection)
        => connection.Disconnect("Client disconnect request");

    /// <summary>
    /// Responds with the current connection status (compression, encryption, etc).
    /// </summary>
    [PacketEncryption(false)]
    [PacketTimeout(Timeouts.Short)]
    [PacketPermission(PermissionLevel.Guest)]
    [PacketRateGroup(nameof(SessionController<TPacket>))]
    [PacketId((ushort)ProtocolCommand.ConnectionStatus)]
    [PacketRateLimit(MaxRequests = 2, LockoutDurationSeconds = 20)]
    internal static System.Memory<byte> GetCurrentModes(IPacket _, IConnection connection)
    {
        ConnInfoDto status = new()
        {
            Compression = connection.Compression,
            Encryption = connection.Encryption
        };

        return PacketWriter.Json(PacketCode.Success, status, NetJsonCxt.Default.ConnInfoDto);
    }
}
