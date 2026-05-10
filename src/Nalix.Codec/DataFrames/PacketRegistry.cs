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
/// Provides the process-wide generated packet registry.
/// </summary>
public static class PacketRegistry
{
    #region Fields

    private static readonly Lock s_gate;

    private static Dictionary<uint, string>? s_pendingNames;
    private static PacketDispatch? s_runtimeFastDispatcher;
    private static List<PacketDispatch>? s_pendingFastDispatchers;

    private static FrozenDictionary<uint, PacketDeserializer>? s_deserializers;
    private static Dictionary<uint, PacketDeserializer>? s_pendingDeserializers;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets whether the registry has been built.
    /// </summary>
    public static bool IsBuilt => Volatile.Read(ref s_deserializers) is not null;

    /// <summary>
    /// A single instance of the Pool Manager shared across all packet types.
    /// If this is <see langword="null"/>, the system automatically falls back to standard allocation.
    /// </summary>
    internal static IObjectPoolManager? Manager;

    /// <summary>
    /// Gets the number of frozen deserializers.
    /// </summary>
    public static int DeserializerCount => GetBuilt().Count;

    #endregion Properties

    #region Constructors

    static PacketRegistry()
    {
        s_gate = new();
        s_pendingNames = new();
        s_pendingDeserializers = new();
        s_pendingFastDispatchers = new();
    }

    #endregion Constructors

    #region APIs

    /// <summary>
    /// Configures the shared Pool Manager for the entire packet ecosystem.
    /// </summary>
    public static void Configure(IObjectPoolManager manager) => Volatile.Write(ref Manager, manager);

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

            // Merge late registrations
            if (s_pendingDeserializers?.Count > 0)
            {
                foreach (KeyValuePair<uint, PacketDeserializer> kv in s_pendingDeserializers)
                {
                    (s_pendingDeserializers ??= new())[kv.Key] = kv.Value;
                }
            }

            s_deserializers = (s_pendingDeserializers ?? new()).ToFrozenDictionary();
            s_runtimeFastDispatcher = COMPOSE(s_pendingFastDispatchers?.ToArray() ?? []);

            // Cleanup
            s_pendingNames = null;
            s_pendingDeserializers = null;
            s_pendingFastDispatchers = null;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static PacketDispatch? COMPOSE(PacketDispatch[] dispatchers)
        {
            return dispatchers.Length switch
            {
                0 => null,
                1 => dispatchers[0],
                _ => (magic, raw, [NotNullWhen(true)] out packet) =>
                {
                    for (int i = 0; i < dispatchers.Length; i++)
                    {
                        if (dispatchers[i](magic, raw, out packet))
                        {
                            return true;
                        }
                    }

                    packet = null;
                    return false;
                }
            };
        }
    }

    /// <summary>
    /// Registers a source-generated fast dispatcher before the registry is built.
    /// </summary>
    public static void RegisterGenerated(PacketDispatch dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);

        lock (s_gate)
        {
            if (s_deserializers is not null)
            {
                s_runtimeFastDispatcher = COMPOSE_COMBINED(s_runtimeFastDispatcher, dispatcher);
                return;
            }

            List<PacketDispatch> dispatchers = s_pendingFastDispatchers ??= new();
            dispatchers.Add(dispatcher);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static PacketDispatch? COMPOSE_COMBINED(PacketDispatch? existing, PacketDispatch newOne)
        {
            if (existing is null)
            {
                return newOne;
            }

            if (existing == newOne)
            {
                return existing;
            }

            return (magic, raw, [NotNullWhen(true)] out packet) =>
            {
                if (existing(magic, raw, out packet))
                {
                    return true;
                }

                return newOne(magic, raw, out packet);
            };
        }

    }

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
                (s_pendingDeserializers ??= new())[magic] = deserializer;
                _ = s_pendingNames?[magic] = name;

                return;
            }

            // Early registration
            Dictionary<uint, PacketDeserializer> dict = s_pendingDeserializers!;
            Dictionary<uint, string> names = s_pendingNames!;

            if (dict.TryGetValue(magic, out _))
            {
                string oldName = names.TryGetValue(magic, out string? n) ? n : "<unknown>";
                if (StringComparer.Ordinal.Equals(oldName, name))
                {
                    return;
                }

                throw new InternalErrorException($"[PacketRegistry] Hash collision! 0x{magic:X8}: {oldName} vs {name}");
            }

            dict[magic] = deserializer;
            names[magic] = name;
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

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static uint Compute(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return Compute(type.FullName ?? type.Name);

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        static uint Compute(string name)
        {
            const uint offset = 2166136261u;
            const uint prime = 16777619u;

            uint hash = offset;

            foreach (char ch in name)
            {
                hash ^= ch;
                hash *= prime;
            }

            return hash;
        }
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
    /// Generated fast dispatchers are authoritative once registered; the dictionary fallback is used only when no
    /// generated dispatcher exists.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool TryDeserialize(ReadOnlySpan<byte> raw, [NotNullWhen(true)] out IPacket? packet)
    {
        if ((uint)raw.Length < PacketConstants.HeaderSize)
        {
            packet = null;
            return false;
        }

        uint magic = raw.AsHeaderRef().MagicNumber;

        try
        {
            PacketDispatch? dispatcher = s_runtimeFastDispatcher;
            if (dispatcher is not null)
            {
                return dispatcher(magic, raw, out packet);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or SerializationFailureException)
        {
            packet = null;
            return false;
        }

        return TRY_DESERIALIZE_FALLBACK(magic, raw, out packet);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool TRY_DESERIALIZE_FALLBACK(uint magic, ReadOnlySpan<byte> raw, [NotNullWhen(true)] out IPacket? packet)
        {
            FrozenDictionary<uint, PacketDeserializer> deserializers = GetBuilt();
            if (!deserializers.TryGetValue(magic, out PacketDeserializer? deserializer))
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
    }

    #endregion APIs

    #region Private Methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static FrozenDictionary<uint, PacketDeserializer> GetBuilt()
    {
        return Volatile.Read(ref s_deserializers)
            ?? throw new InvalidOperationException("PacketRegistry is not built. Call PacketRegistry.Build() after all packet assemblies are loaded.");
    }

    #endregion Private Methods
}

/// <summary>
/// Resolves and deserializes a generated packet without registry dictionary lookup.
/// </summary>
public delegate bool PacketDispatch(uint magic, ReadOnlySpan<byte> raw, [NotNullWhen(true)] out IPacket? packet);
