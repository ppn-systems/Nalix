using Nalix.Common.Connection;
using Nalix.Common.Connection.Protocols;
using Nalix.Common.Constants;
using Nalix.Common.Cryptography;
using Nalix.Common.Logging;
using Nalix.Common.Package;
using Nalix.Common.Package.Attributes;
using Nalix.Common.Package.Enums;
using Nalix.Common.Security;
using Nalix.Serialization;
using Nalix.Shared.Contracts;

namespace Nalix.Shared.Net.Operations;

/// <summary>
/// Provides handlers for managing connection-level configuration commands,
/// such as setting compression and encryption modes during the handshake phase.
/// This controller is designed to be used with Dependency Injection and supports logging.
/// </summary>
[PacketController]
internal sealed class ConnectionOps<TPacket>(ILogger? logger) where TPacket : IPacket, IPacketFactory<TPacket>
{
    private readonly ILogger? _logger = logger;

    /// <summary>
    /// Handles a request to set the encryption mode for the current connection.
    /// The mode is expected as the first byte of the packet's binary payload.
    /// </summary>
    /// <param name="packet">The incoming packet containing the encryption mode.</param>
    /// <param name="connection">The active client connection.</param>
    /// <returns>A response packet indicating success or failure.</returns>
    [PacketEncryption(false)]
    [PacketTimeout(Timeouts.Short)]
    [PacketPermission(PermissionLevel.Guest)]
    [PacketOpcode((ushort)ProtocolCommand.SetEncryptionMode)]
    [PacketRateLimit(RequestLimitType.Low)]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal System.Memory<byte> SetEncryptionMode(TPacket packet, IConnection connection)
        => SetMode<EncryptionType>(packet, connection);

    /// <summary>
    /// Handles a client-initiated disconnect request.
    /// </summary>
    [PacketEncryption(false)]
    [PacketTimeout(Timeouts.Short)]
    [PacketPermission(PermissionLevel.Guest)]
    [PacketOpcode((ushort)ProtocolCommand.Disconnect)]
    [PacketRateLimit(RequestLimitType.Low)]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static void Disconnect(IPacket _, IConnection connection)
        => connection.Disconnect("Net disconnect request");

    /// <summary>
    /// Responds with the current connection status (compression, encryption, etc).
    /// </summary>
    [PacketEncryption(false)]
    [PacketTimeout(Timeouts.Short)]
    [PacketPermission(PermissionLevel.Guest)]
    [PacketOpcode((ushort)ProtocolCommand.ConnectionStatus)]
    [PacketRateLimit(RequestLimitType.Low)]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static System.Memory<byte> GetCurrentModes(IPacket _, IConnection connection)
    {
        ConnInfoDto status = new()
        {
            Encryption = connection.Encryption
        };

        return TPacket.Create(
            (ushort)ProtocolCommand.PingInfo, PacketType.String, PacketFlags.None,
            PacketPriority.Low, JsonCodec.SerializeToMemory(status, NetJsonCxt.Default.ConnInfoDto)).Serialize();
    }

    #region Private Methods

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Memory<byte> SetMode<TEnum>(TPacket packet, IConnection connection)
        where TEnum : struct, System.Enum
    {
        if (packet.Type != PacketType.Binary)
        {
            _logger?.Debug(
                "Invalid packet type [{0}] for SetMode<{1}> from {2}",
                packet.Type, typeof(TEnum).Name, connection.RemoteEndPoint);

            return TPacket.Create((ushort)ProtocolCommand.SetEncryptionMode, "Invalid packet type")
                          .Serialize();
        }

        if (packet.Payload.Length < 1)
        {
            _logger?.Debug(
                "Missing payload byte in SetMode<{0}> from {1}",
                typeof(TEnum).Name, connection.RemoteEndPoint);

            return TPacket.Create((ushort)ProtocolCommand.SetEncryptionMode, "Invalid packet type")
                          .Serialize();
        }

        byte value = packet.Payload.Span[0];

        if (!System.Enum.IsDefined(typeof(TEnum), value))
        {
            _logger?.Debug(
                "Invalid enum value [{0}] in SetMode<{1}> from {2}",
                value, typeof(TEnum).Name, connection.RemoteEndPoint);

            return TPacket.Create((ushort)ProtocolCommand.SetEncryptionMode, "Invalid packet type")
                          .Serialize();
        }
        TEnum enumValue = System.Runtime.CompilerServices.Unsafe.As<byte, TEnum>(ref value);

        if (typeof(TEnum) == typeof(EncryptionType))
            connection.Encryption = System.Runtime.CompilerServices.Unsafe
                .As<TEnum, EncryptionType>(ref enumValue);

        _logger?.Debug(
            "Set {0} to [{1}] for {2}",
            typeof(TEnum).Name, value, connection.RemoteEndPoint);

        return TPacket.Create((ushort)ProtocolCommand.SetEncryptionMode, "Invalid packet type")
                      .Serialize();
    }

    #endregion Private Methods
}
