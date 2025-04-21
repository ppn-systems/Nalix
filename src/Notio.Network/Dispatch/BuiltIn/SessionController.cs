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
    [PacketId((ushort)ProtocolPacket.Disconnect)]
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
    [PacketId((ushort)ProtocolPacket.ConnectionStatus)]
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
