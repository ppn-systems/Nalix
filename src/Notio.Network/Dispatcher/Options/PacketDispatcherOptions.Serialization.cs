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
            _pCompressionMethod = (packet, connection) =>
                packet is TPacket typedPacket
                    ? compressionMethod(typedPacket, connection)
                    : packet;

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
            _pDecompressionMethod = (packet, connection) =>
                packet is TPacket typedPacket
                    ? decompressionMethod(typedPacket, connection)
                    : packet;

            _logger?.Debug($"Type-specific packet decompression configured for {typeof(TPacket).Name}.");
        }

        return this;
    }

    /// <summary>
    /// Configures a type-specific packet serialization method.
    /// </summary>
    /// <param name="serializer">A strongly-typed function that serializes a packet of type <typeparamref name="TPacket"/> into a <see cref="Memory{Byte}"/>.</param>
    /// <returns>The current <see cref="PacketDispatcherOptions{TPacket}"/> instance for method chaining.</returns>
    [Obsolete("This field is no longer used and will be removed in a future version.")]
    public PacketDispatcherOptions<TPacket> WithSerializer(
        Func<TPacket, Memory<byte>> serializer)
    {
        ArgumentNullException.ThrowIfNull(serializer);

        // Create adapter function - check if packet is TPacket before calling serializer
        SerializationMethod = packet =>
        {
            if (packet is TPacket typedPacket) return serializer(typedPacket);

            throw new InvalidOperationException(
                $"Cannot serialize packet of type {packet.GetType().Name}. Expected {typeof(TPacket).Name}.");
        };

        _logger?.Debug($"Type-specific packet serialization configured for {typeof(TPacket).Name}.");
        return this;
    }

    /// <summary>
    /// Configures a type-specific packet deserialization method.
    /// </summary>
    /// <param name="deserializer">A strongly-typed function that deserializes a <see cref="ReadOnlyMemory{Byte}"/> into a packet of type <typeparamref name="TPacket"/>.</param>
    /// <returns>The current <see cref="PacketDispatcherOptions{TPacket}"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if deserializer is null.</exception>
    /// <remarks>
    /// This method provides type safety by ensuring the deserialization process returns the expected packet type.
    /// </remarks>
    public PacketDispatcherOptions<TPacket> WithDeserializer(Func<ReadOnlyMemory<byte>, TPacket> deserializer)
    {
        ArgumentNullException.ThrowIfNull(deserializer);

        DeserializationMethod = bytes => deserializer(bytes);

        _logger?.Debug($"Type-specific packet deserialization configured for {typeof(TPacket).Name}.");
        return this;
    }

    /// <summary>
    /// Deserializes the given byte array into an <see cref="IPacket"/> instance.
    /// </summary>
    /// <param name="bytes">The byte array representing the serialized packet data.</param>
    /// <returns>The deserialized <see cref="IPacket"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the deserialization method is not set.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="bytes"/> is <see langword="null"/>.
    /// </exception>
    public TPacket Deserialize(byte[]? bytes)
    {
        if (this.DeserializationMethod is null)
        {
            _logger?.Error("Deserialize method is not set.");
            throw new InvalidOperationException("Deserialize method is not set.");
        }

        if (bytes is null)
        {
            _logger?.Error("Attempted to deserialize null byte array.");
            throw new ArgumentNullException(nameof(bytes), "Byte array cannot be null.");
        }

        return this.DeserializationMethod(bytes);
    }

    /// <summary>
    /// Deserializes the given byte array into an <see cref="IPacket"/> instance.
    /// </summary>
    /// <param name="bytes">The byte array representing the serialized packet data.</param>
    /// <returns>The deserialized <see cref="IPacket"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the deserialization method is not set.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="bytes"/> is <see langword="null"/>.
    /// </exception>
    public TPacket Deserialize(ReadOnlyMemory<byte>? bytes)
    {
        if (this.DeserializationMethod is null)
        {
            _logger?.Error("Deserialize method is not set.");
            throw new InvalidOperationException("Deserialize method is not set.");
        }

        if (bytes is null || bytes.Value.Length == 0)
        {
            _logger?.Error("Attempted to deserialize null or empty byte array.");
            throw new ArgumentNullException(nameof(bytes), "Byte array cannot be null or empty.");
        }

        return this.DeserializationMethod(bytes.Value);
    }

    /// <summary>
    /// Serializes the given <see cref="IPacket"/> instance into a <see cref="Memory{Byte}"/>.
    /// </summary>
    /// <param name="packet">The packet to be serialized.</param>
    /// <returns>
    /// A <see cref="Memory{Byte}"/> containing the serialized packet data.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the <see cref="SerializationMethod"/> is not set.
    /// </exception>
    [Obsolete("This field is no longer used and will be removed in a future version.")]
    public Memory<byte> Serialize(TPacket packet)
    {
        if (this.SerializationMethod is null)
        {
            _logger?.Error("Serialize method is not set.");
            throw new InvalidOperationException("Serialize method is not set.");
        }

        return this.SerializationMethod(packet);
    }
}
