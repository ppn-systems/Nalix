using Nalix.Common.Connection;
using Nalix.Common.Connection.Contracts;
using Nalix.Common.Constants;
using Nalix.Common.Package;
using Nalix.Common.Package.Attributes;
using Nalix.Common.Package.Enums;
using Nalix.Common.Security;
using Nalix.Serialization;
using System.Runtime.CompilerServices;

namespace Nalix.Shared.Net.Controller;

/// <summary>
/// Provides handlers for managing connection-level configuration commands,
/// such as setting compression and encryption modes during the handshake phase.
/// This controller is designed to be used with Dependency Injection and supports logging.
/// </summary>
[PacketController]
public sealed class SessionController<TPacket> where TPacket : IPacket, IPacketFactory<TPacket>
{
    /// <summary>
    /// Handles a client-initiated disconnect request.
    /// </summary>
    [PacketEncryption(false)]
    [PacketTimeout(Timeouts.Short)]
    [PacketPermission(PermissionLevel.Guest)]
    [PacketRateGroup(nameof(SessionController<TPacket>))]
    [PacketId((ushort)ConnectionCommand.Disconnect)]
    [PacketRateLimit(MaxRequests = 2, LockoutDurationSeconds = 20)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Disconnect(IPacket _, IConnection connection)
        => connection.Disconnect("Net disconnect request");

    /// <summary>
    /// Responds with the current connection status (compression, encryption, etc).
    /// </summary>
    [PacketEncryption(false)]
    [PacketTimeout(Timeouts.Short)]
    [PacketPermission(PermissionLevel.Guest)]
    [PacketRateGroup(nameof(SessionController<TPacket>))]
    [PacketId((ushort)ConnectionCommand.ConnectionStatus)]
    [PacketRateLimit(MaxRequests = 2, LockoutDurationSeconds = 20)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static System.Memory<byte> GetCurrentModes(IPacket _, IConnection connection)
    {
        ConnInfoDto status = new()
        {
            Encryption = connection.Encryption
        };

        return TPacket.Create(
            (ushort)ConnectionCommand.PingInfo, PacketCode.Success, PacketType.String, PacketFlags.None,
            PacketPriority.Low, JsonCodec.SerializeToMemory(status, NetJsonCxt.Default.ConnInfoDto)).Serialize();
    }
}
