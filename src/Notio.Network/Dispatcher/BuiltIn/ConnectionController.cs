using Notio.Common.Compression;
using Notio.Common.Connection;
using Notio.Common.Cryptography;
using Notio.Common.Package;
using Notio.Common.Package.Attributes;
using Notio.Common.Package.Enums;
using Notio.Common.Security;
using Notio.Network.Core;
using Notio.Network.Core.Packets;
using System;

namespace Notio.Network.Dispatcher.BuiltIn;

/// <summary>
/// Provides handlers for managing connection configuration commands, 
/// such as compression and encryption modes.
/// </summary>
[PacketController]
public static class ConnectionController
{
    /// <summary>
    /// Handles a request to set the compression mode for the current connection.
    /// This command is typically sent by a client during connection setup to define how data should be compressed.
    /// </summary>
    /// <param name="packet">The incoming packet containing the compression mode as the first byte of its payload.</param>
    /// <param name="connection">The connection to apply the new compression mode to.</param>
    /// <returns>A response packet indicating success or the reason for failure.</returns>
    [PacketTimeout(5000)]
    [PacketEncryption(false)]
    [PacketPermission(PermissionLevel.Guest)]
    [PacketId((ushort)InternalProtocolCommand.SetCompressionMode)]
    public static Memory<byte> SetCompressionMode(IPacket packet, IConnection connection)
        => SetMode<CompressionMode>(packet, mode => connection.ComMode = mode);

    /// <summary>
    /// Handles a request to set the encryption mode for the current connection.
    /// This is used by clients to choose the encryption strategy for further communication.
    /// </summary>
    /// <param name="packet">The incoming packet containing the encryption mode as the first byte of its payload.</param>
    /// <param name="connection">The connection to apply the new encryption mode to.</param>
    /// <returns>A response packet indicating success or the reason for failure.</returns>
    [PacketTimeout(5000)]
    [PacketEncryption(false)]
    [PacketPermission(PermissionLevel.Guest)]
    [PacketId((ushort)InternalProtocolCommand.SetEncryptionMode)]
    public static Memory<byte> SetEncryptionMode(IPacket packet, IConnection connection)
        => SetMode<EncryptionMode>(packet, mode => connection.EncMode = mode);

    /// <summary>
    /// Validates and applies a mode setting (enum value as byte) received from a packet payload.
    /// This is a generic helper for commands that configure connection-level settings.
    /// </summary>
    /// <typeparam name="TEnum">The enum type representing the mode (e.g., <see cref="CompressionMode"/>, <see cref="EncryptionMode"/>).</typeparam>
    /// <param name="packet">The packet containing the mode value as the first byte of its payload.</param>
    /// <param name="apply">A delegate that applies the parsed enum to the connection.</param>
    /// <returns>A packet indicating success or an error code if validation failed.</returns>
    private static Memory<byte> SetMode<TEnum>(IPacket packet, Action<TEnum> apply)
        where TEnum : struct, Enum
    {
        if (packet.Type != PacketType.Binary)
            return PacketBuilder.String(PacketCode.PacketType);

        if (packet.Payload.Length < 1)
            return PacketBuilder.String(PacketCode.InvalidPayload);

        byte value = packet.Payload.Span[0];

        if (!Enum.IsDefined(typeof(TEnum), value))
            return PacketBuilder.String(PacketCode.InvalidPayload);

        apply((TEnum)(object)value);
        return PacketBuilder.String(PacketCode.Success);
    }
}
