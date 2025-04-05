using Notio.Common.Connection;
using Notio.Common.Package;
using System;

namespace Notio.Network.Dispatcher.Options;

public sealed partial class PacketDispatcherOptions<TPacket> where TPacket : IPacket
{
    /// <summary>
    /// Configures a type-specific packet encryption method.
    /// </summary>
    /// <param name="encryptionMethod">
    /// A function that encrypts a packet of type <typeparamref name="TPacket"/> before sending.
    /// </param>
    /// <returns>The current <see cref="PacketDispatcherOptions{TPacket}"/> instance for method chaining.</returns>
    public PacketDispatcherOptions<TPacket> WithEncryption(
        Func<TPacket, IConnection, TPacket> encryptionMethod)
    {
        if (encryptionMethod is not null)
        {
            _pEncryptionMethod = (packet, connection) =>
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
    /// <param name="decryptionMethod">
    /// A function that decrypts a packet of type <typeparamref name="TPacket"/> before processing.
    /// The function receives the packet and connection context, and returns the decrypted packet.
    /// </param>
    /// <returns>The current <see cref="PacketDispatcherOptions{TPacket}"/> instance for method chaining.</returns>
    public PacketDispatcherOptions<TPacket> WithDecryption(
        Func<TPacket, IConnection, TPacket> decryptionMethod)
    {
        if (decryptionMethod is not null)
        {
            _pDecryptionMethod = (packet, connection) =>
                packet is TPacket typedPacket
                    ? decryptionMethod(typedPacket, connection)
                    : packet;

            _logger?.Debug($"Type-specific packet decryption configured for {typeof(TPacket).Name}.");
        }

        return this;
    }
}
