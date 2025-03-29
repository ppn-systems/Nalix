using Notio.Common.Connection;
using Notio.Common.Package;
using System;

namespace Notio.Network.PacketProcessing.Options;

public sealed partial class PacketDispatcherOptions
{
    /// <summary>
    /// Configures a type-specific packet encryption method.
    /// </summary>
    /// <typeparam name="TPacket">The specific packet type for encryption.</typeparam>
    /// <param name="encryptionMethod">
    /// A function that encrypts a packet of type <typeparamref name="TPacket"/> before sending.
    /// </param>
    /// <returns>The current <see cref="PacketDispatcherOptions"/> instance for method chaining.</returns>
    public PacketDispatcherOptions WithTypedEncryption<TPacket>(
        Func<TPacket, IConnection, TPacket> encryptionMethod)
        where TPacket : IPacket
    {
        if (encryptionMethod is not null)
        {
            _encryptionMethod = (packet, connection) =>
                packet is TPacket typedPacket
                    ? encryptionMethod(typedPacket, connection)
                    : packet;

            _logger?.Debug($"Type-specific packet encryption configured for {typeof(TPacket).Name}.");
        }

        return this;
    }

    /// <summary>
    /// Configures a type-specific packet decryption method.
    /// </summary>
    /// <typeparam name="TPacket">The specific packet type for decryption.</typeparam>
    /// <param name="decryptionMethod">
    /// A function that decrypts a packet of type <typeparamref name="TPacket"/> before processing.
    /// The function receives the packet and connection context, and returns the decrypted packet.
    /// </param>
    /// <returns>The current <see cref="PacketDispatcherOptions"/> instance for method chaining.</returns>
    public PacketDispatcherOptions WithTypedDecryption<TPacket>(
        Func<TPacket, IConnection, TPacket> decryptionMethod)
        where TPacket : IPacket
    {
        if (decryptionMethod is not null)
        {
            _decryptionMethod = (packet, connection) =>
                packet is TPacket typedPacket
                    ? decryptionMethod(typedPacket, connection)
                    : packet;

            _logger?.Debug($"Type-specific packet decryption configured for {typeof(TPacket).Name}.");
        }

        return this;
    }
}
