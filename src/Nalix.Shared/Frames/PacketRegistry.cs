// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Nalix.Common.Networking.Packets;
using Nalix.Shared.Extensions;

namespace Nalix.Shared.Frames;

/// <summary>
/// Provides an immutable, thread-safe catalog of packet deserializers and transformers.
/// </summary>
/// <remarks>
/// <para>
/// The catalog stores lookups for:
/// <list type="bullet">
///   <item>Packet deserializers mapped by 32-bit magic numbers.</item>
///   <item>Packet transformers mapped by concrete packet <see cref="Type"/>.</item>
/// </list>
/// </para>
/// <para>
/// This type is safe for concurrent read access. Instances are immutable once constructed.
/// Both internal dictionaries are <see cref="System.Collections.Frozen.FrozenDictionary{TKey,TValue}"/>
/// for allocation-free, branch-prediction-friendly lookups.
/// </para>
/// </remarks>
public sealed class PacketRegistry : IPacketRegistry
{
    #region Fields

    private readonly System.Collections.Frozen.FrozenDictionary<uint, PacketDeserializer> _deserializers;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketRegistry"/> class using
    /// pre-built frozen lookup tables.
    /// </summary>
    /// <param name="deserializers">
    /// A frozen dictionary mapping magic numbers to <see cref="PacketDeserializer"/> delegates.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when either argument is <see langword="null"/>.
    /// </exception>
    public PacketRegistry(
        System.Collections.Frozen.FrozenDictionary<uint, PacketDeserializer> deserializers)
    {
        ArgumentNullException.ThrowIfNull(deserializers);

        _deserializers = deserializers;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketRegistry"/> class by executing
    /// the specified configuration action on a <see cref="PacketRegistryFactory"/>.
    /// </summary>
    /// <param name="configure">
    /// A delegate that configures the <see cref="PacketRegistryFactory"/> by registering
    /// explicit packet types, assemblies, and/or namespaces. Must not be
    /// <see langword="null"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="configure"/> is <see langword="null"/>.
    /// </exception>
    public PacketRegistry(Action<PacketRegistryFactory> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        PacketRegistryFactory factory = new();
        configure(factory);
        PacketRegistry built = factory.CreateCatalog();

        _deserializers = built._deserializers;
    }

    #endregion Constructors

    #region Diagnostic Properties

    /// <inheritdoc/>
    public int DeserializerCount => _deserializers.Count;

    #endregion Diagnostic Properties

    #region Public API

    /// <inheritdoc/>
    [MethodImpl(
        MethodImplOptions.AggressiveInlining)]
    public bool IsKnownMagic(uint magic) => _deserializers.ContainsKey(magic);

    /// <inheritdoc/>
    [MethodImpl(
        MethodImplOptions.AggressiveInlining)]
    public bool IsRegistered<TPacket>() where TPacket : IPacket => _deserializers.ContainsKey(PacketRegistryFactory.Compute(typeof(TPacket)));

    /// <inheritdoc/>
    public bool TryDeserialize(
        ReadOnlySpan<byte> raw,
        [NotNullWhen(true)] out IPacket? packet)
    {
        if (raw.Length < PacketConstants.HeaderSize)
        {
            packet = null;
            return false;
        }

        uint magic = raw.ReadMagicNumberLE();

        if (_deserializers.TryGetValue(magic, out PacketDeserializer? deserializer))
        {
            packet = deserializer(raw);
            return packet is not null;   // BUG FIX: guard against deserializer returning null
        }

        packet = null;
        return false;
    }

    /// <inheritdoc/>
    public bool TryGetDeserializer(
        uint magic,
        [NotNullWhen(true)] out PacketDeserializer? deserializer)
        => _deserializers.TryGetValue(magic, out deserializer);

    #endregion Public API
}
