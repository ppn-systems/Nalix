// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using Nalix.Abstractions;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Primitives;
using Nalix.Codec.Extensions;

namespace Nalix.Codec.DataFrames;

/// <summary>
/// Resolves and deserializes a generated packet without registry dictionary lookup.
/// </summary>
public delegate bool PacketFastDispatcher(uint magic, ReadOnlySpan<byte> raw, [NotNullWhen(true)] out IPacket? packet);

/// <summary>
/// Provides the process-wide generated packet registry.
/// </summary>
public static class PacketRegistry
{
    private static readonly Lock s_gate = new();
    private static Dictionary<uint, PacketDeserializer>? s_pendingDeserializers = new();
    private static Dictionary<uint, string>? s_pendingNames = new();
    private static FrozenDictionary<uint, PacketDeserializer>? s_deserializers;
    private static List<PacketFastDispatcher>? s_pendingFastDispatchers = new();
    private static PacketFastDispatcher? s_runtimeFastDispatcher;
    private static PacketFastDispatcher[] s_runtimeFastDispatchers = [];

    /// <summary>
    /// A single instance of the Pool Manager shared across all packet types.
    /// If this is <see langword="null"/>, the system automatically falls back to standard allocation.
    /// </summary>
    internal static IObjectPoolManager? Manager;

    /// <summary>
    /// Gets the number of frozen deserializers.
    /// </summary>
    public static int DeserializerCount => GetBuilt().Count;

    /// <summary>
    /// Configures the shared Pool Manager for the entire packet ecosystem.
    /// </summary>
    public static void Configure(IObjectPoolManager manager) => Volatile.Write(ref Manager, manager);

    /// <summary>
    /// Registers a source-generated packet deserializer before the registry is built.
    /// </summary>
    public static void RegisterGenerated(uint magic, string name, PacketDeserializer deserializer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(deserializer);

        lock (s_gate)
        {
            if (s_deserializers is not null)
            {
                if (s_deserializers.ContainsKey(magic))
                {
                    return;
                }

                throw new InvalidOperationException(
                    "PacketRegistry is already built. Load all packet assemblies before first registry access.");
            }

            Dictionary<uint, PacketDeserializer> deserializers = s_pendingDeserializers ??= new();
            Dictionary<uint, string> names = s_pendingNames ??= new();

            if (deserializers.ContainsKey(magic))
            {
                string oldName = names.TryGetValue(magic, out string? resolved) ? resolved : "<unknown>";
                if (StringComparer.Ordinal.Equals(oldName, name))
                {
                    return;
                }

                throw new InternalErrorException(
                    $"[PacketRegistry] Hash collision detected! Magic: 0x{magic:X8}; Type A: {oldName}; Type B: {name}");
            }

            deserializers[magic] = deserializer;
            names[magic] = name;
        }
    }

    /// <summary>
    /// Registers a source-generated fast dispatcher before the registry is built.
    /// </summary>
    public static void RegisterGeneratedDispatcher(PacketFastDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);

        lock (s_gate)
        {
            if (s_deserializers is not null)
            {
                throw new InvalidOperationException(
                    "PacketRegistry is already built. Load all packet assemblies before first registry access.");
            }

            List<PacketFastDispatcher> dispatchers = s_pendingFastDispatchers ??= new();
            dispatchers.Add(dispatcher);
        }
    }

    /// <summary>
    /// Builds and freezes the generated packet registry once.
    /// </summary>
    public static void Build()
    {
        FrozenDictionary<uint, PacketDeserializer>? current = Volatile.Read(ref s_deserializers);
        if (current is not null)
        {
            return;
        }

        lock (s_gate)
        {
            if (s_deserializers is not null)
            {
                return;
            }

            Dictionary<uint, PacketDeserializer> pending = s_pendingDeserializers ?? new();
            PacketFastDispatcher[] dispatchers = s_pendingFastDispatchers?.ToArray() ?? [];
            s_runtimeFastDispatcher = dispatchers.Length == 1 ? dispatchers[0] : null;
            s_runtimeFastDispatchers = dispatchers.Length > 1 ? dispatchers : [];
            s_deserializers = pending.ToFrozenDictionary();
            s_pendingFastDispatchers = null;
            s_pendingDeserializers = null;
            s_pendingNames = null;
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> if a deserializer is registered for the magic number.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsKnownMagic(uint magic) => GetBuilt().ContainsKey(magic);

    /// <summary>
    /// Returns <see langword="true"/> if a deserializer is registered for <typeparamref name="TPacket"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsRegistered<TPacket>() where TPacket : IPacket => GetBuilt().ContainsKey(Compute(typeof(TPacket)));

    /// <summary>
    /// Computes the deterministic packet magic from a packet type full name.
    /// </summary>
    public static uint Compute(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        const uint offset = 2166136261u;
        const uint prime = 16777619u;

        uint hash = offset;
        string name = type.FullName ?? type.Name;
        for (int i = 0; i < name.Length; i++)
        {
            hash ^= name[i];
            hash *= prime;
        }

        return hash;
    }

    /// <summary>
    /// Deserializes a packet by resolving the magic number from the raw buffer.
    /// </summary>
    public static IPacket Deserialize(ReadOnlySpan<byte> raw)
    {
        if (TryDeserialize(raw, out IPacket? packet))
        {
            return packet;
        }

        if ((uint)raw.Length < PacketConstants.HeaderSize)
        {
            throw new ArgumentException(
                $"Raw packet data is too short to contain a valid header. " +
                $"Expected at least {PacketConstants.HeaderSize} bytes, but got {raw.Length}.", nameof(raw));
        }

        ref readonly PacketHeader header = ref raw.AsHeaderRef();
        FrozenDictionary<uint, PacketDeserializer> deserializers = GetBuilt();
        if (!deserializers.TryGetValue(header.MagicNumber, out PacketDeserializer? deserializer))
        {
            throw new InvalidOperationException(
                $"Cannot deserialize packet: Magic 0x{header.MagicNumber:X8} is not registered. " +
                "Check generated packet registration and assembly load order.");
        }

        return deserializer(raw);
    }

    /// <summary>
    /// Attempts to deserialize a packet without throwing for unknown magic or short input.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool TryDeserialize(ReadOnlySpan<byte> raw, [NotNullWhen(true)] out IPacket? packet)
    {
        if ((uint)raw.Length < PacketConstants.HeaderSize)
        {
            packet = null;
            return false;
        }

        ref readonly PacketHeader header = ref raw.AsHeaderRef();
        try
        {
            PacketFastDispatcher? fast = s_runtimeFastDispatcher;
            if (fast is not null && fast(header.MagicNumber, raw, out packet))
            {
                return true;
            }

            PacketFastDispatcher[] fastDispatchers = s_runtimeFastDispatchers;
            for (int i = 0; i < fastDispatchers.Length; i++)
            {
                if (fastDispatchers[i](header.MagicNumber, raw, out packet))
                {
                    return true;
                }
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or SerializationFailureException)
        {
            packet = null;
            return false;
        }

        FrozenDictionary<uint, PacketDeserializer> deserializers = GetBuilt();
        if (!deserializers.TryGetValue(header.MagicNumber, out PacketDeserializer? deserializer))
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

    private static FrozenDictionary<uint, PacketDeserializer> GetBuilt()
    {
        return Volatile.Read(ref s_deserializers)
            ?? throw new InvalidOperationException("PacketRegistry is not built. Call PacketRegistry.Build() after all packet assemblies are loaded.");
    }
}
