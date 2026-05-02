// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Nalix.Abstractions;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Primitives;
using Nalix.Codec.Extensions;

namespace Nalix.Codec.DataFrames;

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

    private readonly FrozenDictionary<uint, PacketDeserializer> _deserializers;

    /// <summary>
    /// A single instance of the Pool Manager shared across all packet types.
    /// If this is <see langword="null"/>, the system automatically falls back to standard allocation.
    /// </summary>
    internal static IObjectPoolManager? Manager;

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
    public PacketRegistry(FrozenDictionary<uint, PacketDeserializer> deserializers)
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

    /// <summary>
    /// Configures the shared Pool Manager for the entire packet ecosystem.
    /// </summary>
    /// <param name="manager">An implementation of <see cref="IObjectPoolManager"/> from the infrastructure layer.</param>
    /// <remarks>
    /// Because <see cref="PacketRegistry"/> is shared, calling Configure on any 
    /// <c>PacketProvider&lt;T&gt;</c> will take effect for all other packet types.
    /// </remarks>
    public static void Configure(IObjectPoolManager manager) => System.Threading.Volatile.Write(ref Manager, manager);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsKnownMagic(uint magic) => _deserializers.ContainsKey(magic);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsRegistered<TPacket>() where TPacket : IPacket => _deserializers.ContainsKey(PacketRegistryFactory.Compute(typeof(TPacket)));

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">Thrown when a registered deserializer attempts to read a malformed packet header.</exception>
    public IPacket Deserialize(ReadOnlySpan<byte> raw)
    {
        if (this.TryDeserialize(raw, out IPacket? packet))
        {
            return packet;
        }

        if (raw.Length < PacketConstants.HeaderSize)
        {
            throw new ArgumentException(
                $"Raw packet data is too short to contain a valid header. " +
                $"Expected at least {PacketConstants.HeaderSize} bytes, but got {raw.Length}.", nameof(raw));
        }

        uint magic = raw.ReadHeaderLE().MagicNumber;

        if (!_deserializers.TryGetValue(magic, out PacketDeserializer? deserializer))
        {
            throw new InvalidOperationException(
                $"Cannot deserialize packet: Magic 0x{magic:X8} is not registered. " +
                $"Check your PacketRegistryFactory configuration.");
        }

        return deserializer(raw);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool TryDeserialize(ReadOnlySpan<byte> raw, [NotNullWhen(true)] out IPacket? packet)
    {
        if (raw.Length < PacketConstants.HeaderSize)
        {
            packet = null;
            return false;
        }

        uint magic = raw.ReadHeaderLE().MagicNumber;
        if (!_deserializers.TryGetValue(magic, out PacketDeserializer? deserializer) || deserializer is null)
        {
            packet = null;
            return false;
        }

        try
        {
            packet = deserializer(raw);
            return packet is not null;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or SerializationFailureException)
        {
            packet = null;
            return false;
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool TryReadHeader(ReadOnlySpan<byte> raw, out PacketHeader header)
    {
        if (raw.Length < PacketConstants.HeaderSize)
        {
            header = default;
            return false;
        }

        header = raw.ReadHeaderLE();
        return true;
    }

    #endregion Public API
}
