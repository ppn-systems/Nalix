using Notio.Common.Compression;
using Notio.Common.Connection;
using Notio.Common.Constants;
using Notio.Common.Cryptography;
using Notio.Common.Logging;
using Notio.Common.Package;
using Notio.Common.Package.Attributes;
using Notio.Common.Package.Enums;
using Notio.Common.Security;
using Notio.Network.Dispatch.BuiltIn.Internal;
using System.Runtime.CompilerServices;

namespace Notio.Network.Dispatch.BuiltIn;

/// <summary>
/// Handles connection mode settings for compression and encryption.
/// </summary>
[PacketController]
public sealed class ModeController<TPacket>(ILogger? logger) where TPacket : IPacket
{
    #region Fields

    private readonly ILogger? _logger = logger;

    #endregion

    /// <summary>
    /// Handles a request to set the compression mode for the current connection.
    /// The mode is expected as the first byte of the packet's binary payload.
    /// </summary>
    /// <param name="packet">The incoming packet containing the compression mode.</param>
    /// <param name="connection">The active client connection.</param>
    /// <returns>A response packet indicating success or failure.</returns>
    [PacketEncryption(false)]
    [PacketTimeout(Timeouts.Short)]
    [PacketPermission(PermissionLevel.Guest)]
    [PacketRateGroup(nameof(SessionController<TPacket>))]
    [PacketId((ushort)ProtocolCommand.SetCompressionMode)]
    [PacketRateLimit(MaxRequests = 1, LockoutDurationSeconds = 100)]
    internal System.Memory<byte> SetCompressionMode(TPacket packet, IConnection connection)
        => SetMode<CompressionType>(packet, connection);

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
    [PacketRateGroup(nameof(SessionController<TPacket>))]
    [PacketId((ushort)ProtocolCommand.SetEncryptionMode)]
    [PacketRateLimit(MaxRequests = 1, LockoutDurationSeconds = 100)]
    internal System.Memory<byte> SetEncryptionMode(TPacket packet, IConnection connection)
        => SetMode<EncryptionType>(packet, connection);

    #region Private Methods

    private System.Memory<byte> SetMode<TEnum>(TPacket packet, IConnection connection)
        where TEnum : struct, System.Enum
    {
        if (packet.Type != PacketType.Binary)
        {
            _logger?.Debug("Invalid packet type [{0}] for SetMode<{1}> from {2}",
                packet.Type, typeof(TEnum).Name, connection.RemoteEndPoint);
            return PacketWriter.String(PacketCode.PacketType);
        }

        if (packet.Payload.Length < 1)
        {
            _logger?.Debug("Missing payload byte in SetMode<{0}> from {1}",
                typeof(TEnum).Name, connection.RemoteEndPoint);
            return PacketWriter.String(PacketCode.InvalidPayload);
        }

        byte value = packet.Payload.Span[0];

        if (!System.Enum.IsDefined(typeof(TEnum), value))
        {
            _logger?.Debug("Invalid enum value [{0}] in SetMode<{1}> from {2}",
                value, typeof(TEnum).Name, connection.RemoteEndPoint);
            return PacketWriter.String(PacketCode.InvalidPayload);
        }
        TEnum enumValue = Unsafe.As<byte, TEnum>(ref value);

        if (typeof(TEnum) == typeof(CompressionType))
            connection.Compression = Unsafe.As<TEnum, CompressionType>(ref enumValue);
        else if (typeof(TEnum) == typeof(EncryptionType))
            connection.Encryption = Unsafe.As<TEnum, EncryptionType>(ref enumValue);

        _logger?.Debug("Set {0} to [{1}] for {2}", typeof(TEnum).Name, value, connection.RemoteEndPoint);
        return PacketWriter.String(PacketCode.Success);
    }

    #endregion
}
