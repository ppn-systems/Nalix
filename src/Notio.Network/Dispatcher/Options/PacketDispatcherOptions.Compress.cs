using Notio.Common.Connection;
using Notio.Common.Package;
using System;

namespace Notio.Network.Dispatcher.Options;

public sealed partial class PacketDispatcherOptions<TPacket> where TPacket : IPacket
{
    /// <summary>
    /// Configures a custom compression strategy for outgoing packets of type <typeparamref name="TPacket"/>.
    /// </summary>
    /// <param name="compressionMethod">
    /// A delegate that takes a packet and its associated <see cref="IConnection"/>, returning a compressed version of the packet.
    /// </param>
    /// <returns>
    /// Returns the current <see cref="PacketDispatcherOptions{TPacket}"/> instance, allowing for fluent configuration chaining.
    /// </returns>
    /// <remarks>
    /// This method is useful for reducing packet size before transmission. The provided delegate will be invoked
    /// right before the packet is dispatched through the connection.
    /// </remarks>
    public PacketDispatcherOptions<TPacket> WithCompression(Func<TPacket, IConnection, TPacket> compressionMethod)
    {
        if (compressionMethod is not null)
        {
            _pCompressionMethod = compressionMethod;

            _logger?.Debug($"Type-specific packet compression configured for {typeof(TPacket).Name}.");
        }

        return this;
    }

    /// <summary>
    /// Configures a custom decompression strategy for incoming packets of type <typeparamref name="TPacket"/>.
    /// </summary>
    /// <param name="decompressionMethod">
    /// A delegate that takes a packet and its associated <see cref="IConnection"/>, returning a decompressed version of the packet.
    /// </param>
    /// <returns>
    /// Returns the current <see cref="PacketDispatcherOptions{TPacket}"/> instance, allowing for fluent configuration chaining.
    /// </returns>
    /// <remarks>
    /// This method allows you to handle packets that were compressed prior to transmission.
    /// The provided delegate is invoked immediately after deserialization and before handler execution.
    /// </remarks>
    public PacketDispatcherOptions<TPacket> WithDecompression(Func<TPacket, IConnection, TPacket> decompressionMethod)
    {
        if (decompressionMethod is not null)
        {
            _pDecompressionMethod = decompressionMethod;

            _logger?.Debug($"Type-specific packet decompression configured for {typeof(TPacket).Name}.");
        }

        return this;
    }
}
