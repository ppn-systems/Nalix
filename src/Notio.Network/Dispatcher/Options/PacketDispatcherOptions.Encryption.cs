using Notio.Common.Connection;
using Notio.Common.Package;
using System;

namespace Notio.Network.Dispatcher.Options;

public sealed partial class PacketDispatcherOptions<TPacket> where TPacket : IPacket
{
    /// <summary>
    /// Configures a custom encryption function for outgoing packets of type <typeparamref name="TPacket"/>.
    /// </summary>
    /// <param name="encryptionMethod">
    /// A function that takes the original packet and connection context, then returns an encrypted version of the packet.
    /// </param>
    /// <returns>
    /// The current <see cref="PacketDispatcherOptions{TPacket}"/> instance to allow method chaining.
    /// </returns>
    /// <remarks>
    /// This method enables encryption for outgoing packets. The encryption logic is fully customizable
    /// and is applied before sending the packet to the remote endpoint.
    /// </remarks>
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
    /// Configures a custom decryption function for incoming packets of type <typeparamref name="TPacket"/>.
    /// </summary>
    /// <param name="decryptionMethod">
    /// A function that takes the received packet and connection context, then returns a decrypted version of the packet.
    /// </param>
    /// <returns>
    /// The current <see cref="PacketDispatcherOptions{TPacket}"/> instance to allow method chaining.
    /// </returns>
    /// <remarks>
    /// This method enables decryption for incoming packets. The provided function is invoked
    /// immediately after deserialization and before dispatching to the registered handler.
    /// </remarks>
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
