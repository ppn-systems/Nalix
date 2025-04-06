using Notio.Common.Connection;
using Notio.Common.Package;
using System;

namespace Notio.Network.Dispatcher.Options;

public sealed partial class PacketDispatcherOptions<TPacket> where TPacket : IPacket
{
    /// <summary>
    /// Configures a type-specific packet compression method.
    /// </summary>
    /// <param name="compressionMethod">
    /// A function that compresses a packet of type <typeparamref name="TPacket"/> before sending.
    /// </param>
    /// <returns>The current <see cref="PacketDispatcherOptions{TPacket}"/> instance for method chaining.</returns>
    public PacketDispatcherOptions<TPacket> WithCompression(
        Func<TPacket, IConnection, TPacket> compressionMethod)
    {
        if (compressionMethod is not null)
        {
            _pCompressionMethod = compressionMethod;

            _logger?.Debug($"Type-specific packet compression configured for {typeof(TPacket).Name}.");
        }

        return this;
    }

    /// <summary>
    /// Configures a type-specific packet decompression method.
    /// </summary>
    /// <param name="decompressionMethod">
    /// A function that decompresses a packet of type <typeparamref name="TPacket"/> before processing.
    /// </param>
    /// <returns>The current <see cref="PacketDispatcherOptions{TPacket}"/> instance for method chaining.</returns>
    public PacketDispatcherOptions<TPacket> WithDecompression(
        Func<TPacket, IConnection, TPacket> decompressionMethod)
    {
        if (decompressionMethod is not null)
        {
            _pDecompressionMethod = decompressionMethod;

            _logger?.Debug($"Type-specific packet decompression configured for {typeof(TPacket).Name}.");
        }

        return this;
    }
}
