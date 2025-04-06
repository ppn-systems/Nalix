using Notio.Common.Connection;
using Notio.Common.Package;
using System;

namespace Notio.Network.Dispatcher.Options;

public sealed partial class PacketDispatcherOptions<TPacket> where TPacket : IPacket
{
    /// <summary>
    /// A function that serializes an <see cref="IPacket"/> into a <see cref="ReadOnlyMemory{Byte}"/>.
    /// </summary>
    /// <remarks>
    /// This function is used to convert an <see cref="IPacket"/> object into a byte array representation
    /// for transmission over the network or for storage.
    /// </remarks>
    [Obsolete("This field is no longer used and will be removed in a future version.")]
    private Func<TPacket, Memory<byte>>? SerializationMethod;

    /// <summary>
    /// A function that deserializes a <see cref="Memory{Byte}"/> into an <see cref="IPacket"/>.
    /// </summary>
    /// <remarks>
    /// This function is responsible for converting the byte array received over the network or from storage
    /// back into an <see cref="IPacket"/> object for further processing.
    /// </remarks>
    [Obsolete("This field is no longer used and will be removed in a future version.")]
    private Func<ReadOnlyMemory<byte>, TPacket>? DeserializationMethod;

    /// <summary>
    /// Configures packet compression and decompression for the packet dispatcher.
    /// </summary>
    /// <param name="compressionMethod">
    /// A function that compresses a packet before sending. The function receives an <see cref="IPacket"/>
    /// and returns the compressed <see cref="IPacket"/>. If this is null, compression will not be applied.
    /// </param>
    /// <param name="decompressionMethod">
    /// A function that decompresses a packet before processing. The function receives an <see cref="IPacket"/>
    /// and returns the decompressed <see cref="IPacket"/>. If this is null, decompression will not be applied.
    /// </param>
    /// <remarks>
    /// This method allows you to specify compression and decompression functions that will be applied to packets
    /// before they are sent or received. The compression and decompression methods are applied based on packet flags,
    /// which help determine if a packet should be compressed or decompressed. If either method is null, the corresponding
    /// compression or decompression step will be skipped.
    /// </remarks>
    /// <returns>
    /// The current <see cref="PacketDispatcherOptions{TPacket}"/> instance for method chaining.
    /// </returns>
    [Obsolete("Use WithCompression and WithDecompression for type-specific compression.", true)]
    public PacketDispatcherOptions<TPacket> WithPacketCompression
    (
        Func<TPacket, IConnection, TPacket>? compressionMethod,
        Func<TPacket, IConnection, TPacket>? decompressionMethod
    )
    {
        if (compressionMethod is not null) _pCompressionMethod = compressionMethod;
        if (decompressionMethod is not null) _pDecompressionMethod = decompressionMethod;

        _logger?.Debug("Packet compression configured.");
        return this;
    }

    /// <summary>
    /// Configures packet encryption and decryption for the packet dispatcher.
    /// </summary>
    /// <param name="encryptionMethod">
    /// A function that encrypts a packet before sending. The function receives an <see cref="IPacket"/> and a byte array (encryption key),
    /// and returns the encrypted <see cref="IPacket"/>.
    /// </param>
    /// <param name="decryptionMethod">
    /// A function that decrypts a packet before processing. The function receives an <see cref="IPacket"/> and a byte array (decryption key),
    /// and returns the decrypted <see cref="IPacket"/>.
    /// </param>
    /// <remarks>
    /// This method allows you to specify encryption and decryption functions that will be applied to packets
    /// before they are sent or received. The encryption and decryption methods will be invoked based on certain conditions,
    /// which are determined by the packet's flags (as checked by <see cref="IPacket.Flags"/>).
    /// Ensure that the encryption and decryption functions are compatible with the packet's structure.
    /// </remarks>
    /// <returns>
    /// The current <see cref="PacketDispatcherOptions{TPacket}"/> instance for method chaining.
    /// </returns>
    [Obsolete("Use WithEncryption and WithDecryption for type-specific encryption.", true)]
    public PacketDispatcherOptions<TPacket> WithPacketCrypto
    (
        Func<TPacket, IConnection, TPacket>? encryptionMethod,
        Func<TPacket, IConnection, TPacket>? decryptionMethod
    )
    {
        if (encryptionMethod is not null) _pEncryptionMethod = encryptionMethod;
        if (decryptionMethod is not null) _pDecryptionMethod = decryptionMethod;

        _logger?.Debug("Packet encryption configured.");
        return this;
    }

    /// <summary>
    /// Configures the packet serialization and deserialization methods.
    /// </summary>
    /// <param name="serializationMethod">
    /// A function that serializes a packet into a <see cref="Memory{Byte}"/>.
    /// </param>
    /// <param name="deserializationMethod">
    /// A function that deserializes a <see cref="Memory{Byte}"/> back into an <see cref="IPacket"/>.
    /// </param>
    /// <returns>
    /// The current <see cref="PacketDispatcherOptions{TPacket}"/> instance for method chaining.
    /// </returns>
    /// <remarks>
    /// This method allows customizing how packets are serialized before sending and deserialized upon receiving.
    /// </remarks>
    [Obsolete("Use WithSerializer and WithDeserializer for type-specific serialization.", true)]
    public PacketDispatcherOptions<TPacket> WithPacketSerialization
    (
        Func<TPacket, Memory<byte>>? serializationMethod,
        Func<ReadOnlyMemory<byte>, TPacket>? deserializationMethod
    )
    {
        if (serializationMethod is not null) SerializationMethod = serializationMethod;
        if (deserializationMethod is not null) DeserializationMethod = deserializationMethod;

        _logger?.Debug("Packet serialization configured.");
        return this;
    }

    /// <summary>
    /// Configures a type-specific packet serialization method.
    /// </summary>
    /// <param name="serializer">A strongly-typed function that serializes a packet of type <typeparamref name="TPacket"/> into a <see cref="Memory{Byte}"/>.</param>
    /// <returns>The current <see cref="PacketDispatcherOptions{TPacket}"/> instance for method chaining.</returns>
    [Obsolete("This field is no longer used and will be removed in a future version.", true)]
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
    /// Serializes the given <see cref="IPacket"/> instance into a <see cref="Memory{Byte}"/>.
    /// </summary>
    /// <param name="packet">The packet to be serialized.</param>
    /// <returns>
    /// A <see cref="Memory{Byte}"/> containing the serialized packet data.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the <see cref="SerializationMethod"/> is not set.
    /// </exception>
    [Obsolete("This field is no longer used and will be removed in a future version.", true)]
    public Memory<byte> Serialize(TPacket packet)
    {
        if (this.SerializationMethod is null)
        {
            _logger?.Error("Serialize method is not set.");
            throw new InvalidOperationException("Serialize method is not set.");
        }

        return this.SerializationMethod(packet);
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
    [Obsolete("This field is no longer used and will be removed in a future version.", true)]
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
    [Obsolete("This field is no longer used and will be removed in a future version.", true)]
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
    [Obsolete("This field is no longer used and will be removed in a future version.", true)]
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
}
