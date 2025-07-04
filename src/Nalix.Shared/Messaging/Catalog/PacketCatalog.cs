// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Packets;
using Nalix.Common.Packets.Abstractions;
using Nalix.Shared.Extensions;

namespace Nalix.Shared.Messaging.Catalog;

/// <summary>
/// Provides an immutable, thread-safe catalog of packet deserializers and transformers.
/// </summary>
/// <remarks>
/// <para>
/// The catalog stores lookups for:
/// <list type="bullet">
///   <item>
///     <description>Packet deserializers mapped by 32-bit magic numbers.</description>
///   </item>
///   <item>
///     <description>Packet transformers mapped by concrete packet <see cref="System.Type"/>.</description>
///   </item>
/// </list>
/// </para>
/// <para>
/// This type is safe for concurrent read access. Instances are immutable once constructed.
/// </para>
/// </remarks>
public sealed class PacketCatalog : IPacketCatalog
{
    private readonly System.Collections.Frozen.FrozenDictionary<System.Type, PacketTransformer> _transformers;
    private readonly System.Collections.Frozen.FrozenDictionary<System.UInt32, PacketDeserializer> _deserializers;

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketCatalog"/> class using the specified lookup tables.
    /// </summary>
    /// <param name="transformers">
    /// A frozen dictionary that maps packet <see cref="System.Type"/> objects to <see cref="PacketTransformer"/> delegates.
    /// </param>
    /// <param name="deserializers">
    /// A frozen dictionary that maps magic numbers to <see cref="PacketDeserializer"/> delegates.
    /// </param>
    /// <remarks>
    /// Both dictionaries are assumed to be non-null and already frozen. The constructor does not copy the inputs.
    /// </remarks>
    public PacketCatalog(
        System.Collections.Frozen.FrozenDictionary<System.Type, PacketTransformer> transformers,
        System.Collections.Frozen.FrozenDictionary<System.UInt32, PacketDeserializer> deserializers)
    {
        _transformers = transformers;
        _deserializers = deserializers;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketCatalog"/> class by executing
    /// the specified configuration action on a <see cref="PacketCatalogFactory"/>.
    /// </summary>
    /// <param name="action">
    /// A delegate that configures the <see cref="PacketCatalogFactory"/> by registering
    /// explicit packet types and/or assemblies. Must not be <see langword="null"/>.
    /// </param>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown if <paramref name="action"/> is <see langword="null"/>.
    /// </exception>
    public PacketCatalog(System.Action<PacketCatalogFactory> action)
    {
        System.ArgumentNullException.ThrowIfNull(action);
        PacketCatalogFactory factory = new();
        action(factory);

        PacketCatalog built = factory.CreateCatalog();

        _transformers = built._transformers;
        _deserializers = built._deserializers;
    }

    /// <summary>
    /// Attempts to deserialize a packet by reading the magic number from the provided raw buffer
    /// and dispatching to a registered deserializer.
    /// </summary>
    /// <param name="raw">
    /// The raw byte span containing the serialized packet. The first four bytes are interpreted
    /// as a little-endian 32-bit magic number.
    /// </param>
    /// <param name="packet">
    /// When this method returns <see langword="true"/>, contains the deserialized <see cref="IPacket"/> instance;
    /// otherwise, contains <see langword="null"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if a matching deserializer is found and the packet is deserialized successfully;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// The method returns <see langword="false"/> if the buffer is shorter than four bytes or if the magic number
    /// does not match any registered deserializer.
    /// </remarks>
    public System.Boolean TryDeserialize(
        [System.Diagnostics.CodeAnalysis.NotNull] System.ReadOnlySpan<System.Byte> raw,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IPacket? packet)
    {
        if (raw.Length < PacketConstants.HeaderSize)
        {
            packet = null;
            return false;
        }

        if (_deserializers.TryGetValue(raw.ReadMagicNumberLE(), out PacketDeserializer? factory))
        {
            packet = factory(raw);
            return true;
        }

        packet = null;
        return false;
    }

    /// <summary>
    /// Attempts to get the <see cref="PacketDeserializer"/> associated with the specified magic number.
    /// </summary>
    /// <param name="magic">The 32-bit magic number that identifies a packet format.</param>
    /// <param name="deserializer">
    /// When this method returns <see langword="true"/>, contains the deserializer delegate;
    /// otherwise, contains <see langword="null"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if a deserializer is registered for the given magic number; otherwise, <see langword="false"/>.
    /// </returns>
    public System.Boolean TryGetDeserializer(
        [System.Diagnostics.CodeAnalysis.NotNull] System.UInt32 magic,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out PacketDeserializer? deserializer)
    {
        System.Boolean ok = _deserializers.TryGetValue(magic, out PacketDeserializer? d);
        deserializer = ok ? d : null;
        return ok;
    }

    /// <summary>
    /// Attempts to get the <see cref="PacketTransformer"/> delegates associated with the specified packet type.
    /// </summary>
    /// <param name="packetType">The concrete packet <see cref="System.Type"/>.</param>
    /// <param name="transformer">
    /// When this method returns <see langword="true"/>, contains the <see cref="PacketTransformer"/> for the type;
    /// otherwise, contains the default value.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if a transformer set is registered for the specified type; otherwise, <see langword="false"/>.
    /// </returns>
    public System.Boolean TryGetTransformer(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Type packetType,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out PacketTransformer transformer)
        => _transformers.TryGetValue(packetType, out transformer);
}
