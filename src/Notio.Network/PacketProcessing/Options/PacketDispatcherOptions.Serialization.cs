using Notio.Common.Connection;
using Notio.Common.Package;
using System;

namespace Notio.Network.PacketProcessing.Options;

public sealed partial class PacketDispatcherOptions
{
    /// <summary>
    /// Configures a type-specific packet compression method.
    /// </summary>
    /// <typeparam name="TPacket">The specific packet type for compression.</typeparam>
    /// <param name="compressionMethod">
    /// A function that compresses a packet of type <typeparamref name="TPacket"/> before sending.
    /// </param>
    /// <returns>The current <see cref="PacketDispatcherOptions"/> instance for method chaining.</returns>
    public PacketDispatcherOptions WithTypedCompression<TPacket>(
        Func<TPacket, IConnection, TPacket> compressionMethod)
        where TPacket : IPacket
    {
        if (compressionMethod is not null)
        {
            _compressionMethod = (packet, connection) =>
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
    /// <typeparam name="TPacket">The specific packet type for decompression.</typeparam>
    /// <param name="decompressionMethod">
    /// A function that decompresses a packet of type <typeparamref name="TPacket"/> before processing.
    /// </param>
    /// <returns>The current <see cref="PacketDispatcherOptions"/> instance for method chaining.</returns>
    public PacketDispatcherOptions WithTypedDecompression<TPacket>(
        Func<TPacket, IConnection, TPacket> decompressionMethod)
        where TPacket : IPacket
    {
        if (decompressionMethod is not null)
        {
            _decompressionMethod = (packet, connection) =>
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
    /// <typeparam name="TPacket">The specific packet type for serialization.</typeparam>
    /// <param name="serializer">A strongly-typed function that serializes a packet of type <typeparamref name="TPacket"/> into a <see cref="Memory{Byte}"/>.</param>
    /// <returns>The current <see cref="PacketDispatcherOptions"/> instance for method chaining.</returns>
    public PacketDispatcherOptions WithTypedSerializer<TPacket>(
        Func<TPacket, Memory<byte>> serializer)
        where TPacket : IPacket
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
    /// <typeparam name="TPacket">The specific packet type for deserialization.</typeparam>
    /// <param name="deserializer">A strongly-typed function that deserializes a <see cref="ReadOnlyMemory{Byte}"/> into a packet of type <typeparamref name="TPacket"/>.</param>
    /// <returns>The current <see cref="PacketDispatcherOptions"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if deserializer is null.</exception>
    /// <remarks>
    /// This method provides type safety by ensuring the deserialization process returns the expected packet type.
    /// </remarks>
    public PacketDispatcherOptions WithTypedDeserializer<TPacket>(
        Func<ReadOnlyMemory<byte>, TPacket> deserializer)
        where TPacket : IPacket
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
    public IPacket Deserialization(byte[]? bytes)
    {
        if (this.DeserializationMethod is null)
        {
            _logger?.Error("Deserialization method is not set.");
            throw new InvalidOperationException("Deserialization method is not set.");
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
    public IPacket Deserialization(ReadOnlyMemory<byte>? bytes)
    {
        if (this.DeserializationMethod is null)
        {
            _logger?.Error("Deserialization method is not set.");
            throw new InvalidOperationException("Deserialization method is not set.");
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
    public Memory<byte> Serialization(IPacket packet)
    {
        if (this.SerializationMethod is null)
        {
            _logger?.Error("Serialization method is not set.");
            throw new InvalidOperationException("Serialization method is not set.");
        }

        return this.SerializationMethod(packet);
    }
}
