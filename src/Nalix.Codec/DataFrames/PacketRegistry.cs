// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using Nalix.Abstractions;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Primitives;
using Nalix.Codec.ProtocolFrames;
using Nalix.Environment.Extensions;

namespace Nalix.Codec.DataFrames;

/// <summary>
/// Provides the process-wide generated packet registry.
/// </summary>
public static class PacketRegistry
{
    #region Fields

    private static readonly Lock s_gate;

    private static Dictionary<ushort, string>? s_pendingNames;
    private static PacketDispatch? s_runtimeFastDispatcher;
    private static List<PacketDispatch>? s_pendingFastDispatchers;

    private static PacketDeserializer?[]? s_table;
    private static Dictionary<ushort, PacketDeserializer>? s_pendingDeserializers;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets whether the registry has been built.
    /// </summary>
    public static bool IsBuilt => Volatile.Read(ref s_table) is not null;

    /// <summary>
    /// A single instance of the Pool Manager shared across all packet types.
    /// If this is <see langword="null"/>, the system automatically falls back to standard allocation.
    /// </summary>
    internal static IObjectPoolManager? Manager;

    /// <summary>
    /// Gets the number of frozen deserializers.
    /// </summary>
    public static int DeserializerCount { get; private set; }

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
    public static void Configure(IObjectPoolManager? manager)
    {
        Volatile.Write(ref Manager, manager);

        if (manager is null)
        {
            return;
        }

        _ = manager.SetMaxCapacity<Control>(128);
        _ = manager.SetMaxCapacity<TimeSync>(128);
        _ = manager.SetMaxCapacity<Directive>(128);

        _ = manager.SetMaxCapacity<ProofOfWorkProof>(128);
        _ = manager.SetMaxCapacity<ProofOfWorkChallenge>(128);

        _ = manager.SetMaxCapacity<SessionTofu>(128);
        _ = manager.SetMaxCapacity<SessionInit>(128);
        _ = manager.SetMaxCapacity<SessionProof>(128);
        _ = manager.SetMaxCapacity<SessionResume>(128);
        _ = manager.SetMaxCapacity<SessionChallenge>(128);
        _ = manager.SetMaxCapacity<SessionEstablished>(128);
    }

    /// <summary>
    /// Builds and freezes the generated packet registry once.
    /// </summary>
    public static void Build()
    {
        PacketDeserializer?[]? current = Volatile.Read(ref s_table);
        if (current is not null)
        {
            return;
        }

        lock (s_gate)
        {
            if (s_table is not null)
            {
                return;
            }

            // Merge late registrations
            if (s_pendingDeserializers?.Count > 0)
            {
                foreach (KeyValuePair<ushort, PacketDeserializer> kv in s_pendingDeserializers)
                {
                    (s_pendingDeserializers ??= new())[kv.Key] = kv.Value;
                }
            }

            PacketDeserializer?[] table = new PacketDeserializer?[65536];
            int count = 0;
            if (s_pendingDeserializers is not null)
            {
                foreach (KeyValuePair<ushort, PacketDeserializer> kv in s_pendingDeserializers)
                {
                    table[kv.Key] = kv.Value;
                    count++;
                }
            }

            DeserializerCount = count;
            s_runtimeFastDispatcher = COMPOSE(s_pendingFastDispatchers?.ToArray() ?? []);
            Volatile.Write(ref s_table, table);

            // Cleanup fast dispatchers since they are fully merged.
            // Do NOT nullify s_pendingNames and s_pendingDeserializers to allow late collision detection.
            s_pendingFastDispatchers = null;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static PacketDispatch? COMPOSE(PacketDispatch[] dispatchers)
        {
            return dispatchers.Length switch
            {
                0 => null,
                1 => dispatchers[0],
                _ => (opcode, raw, [NotNullWhen(true)] out packet) =>
                {
                    for (int i = 0; i < dispatchers.Length; i++)
                    {
                        if (dispatchers[i](opcode, raw, out packet))
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
            if (s_table is not null)
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

            return (opcode, raw, [NotNullWhen(true)] out packet) =>
            {
                if (existing(opcode, raw, out packet))
                {
                    return true;
                }

                return newOne(opcode, raw, out packet);
            };
        }

    }

    /// <summary>
    /// Registers a source-generated packet deserializer before the registry is built.
    /// </summary>
    public static void RegisterGenerated<TPacket>(string name, PacketDeserializer deserializer) where TPacket : IPacket, IPacketStaticOpcode
    {
        ushort opcode = TPacket.StaticOpCode;
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(deserializer);

        lock (s_gate)
        {
            PacketDeserializer?[]? table = s_table;
            if (table is not null)
            {
                THROW_IF_OPCODE_COLLISION(opcode, name, s_pendingNames);

                Volatile.Write(ref table[opcode], deserializer);
                (s_pendingDeserializers ??= new())[opcode] = deserializer;
                (s_pendingNames ??= new())[opcode] = name;

                return;
            }

            // Early registration
            THROW_IF_OPCODE_COLLISION(opcode, name, s_pendingNames);

            s_pendingDeserializers![opcode] = deserializer;
            s_pendingNames![opcode] = name;
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> if a deserializer is registered for the operation code.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsKnownOpCode(ushort opcode) => GetBuilt()[opcode] is not null;

    /// <summary>
    /// Returns <see langword="true"/> if a deserializer is registered for <typeparamref name="TPacket"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsRegistered<TPacket>() where TPacket : IPacket, IPacketStaticOpcode => GetBuilt()[TPacket.StaticOpCode] is not null;

    /// <summary>
    /// Deserializes a packet by resolving the operation code from the raw buffer.
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
                $"Raw packet data is too short to contain a valid header. Expected at least {PacketConstants.HeaderSize} bytes, but got {raw.Length}.", nameof(raw));
        }

        ref readonly PacketHeader header = ref raw.AsHeaderRef();
        PacketDeserializer?[] table = GetBuilt();
        PacketDeserializer? deserializer = Volatile.Read(ref table[header.OpCode]) ?? throw new InvalidOperationException(
                $"Cannot deserialize packet: OpCode {header.OpCode} is not registered. " +
                "Check generated packet registration and assembly load order.");

        return deserializer(raw);
    }

    /// <summary>
    /// Attempts to deserialize a packet without throwing for unknown OpCode or short input.
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

        ushort opcode = raw.AsHeaderRef().OpCode;

        PacketDispatch? dispatcher = Volatile.Read(ref s_runtimeFastDispatcher);
        if (dispatcher is not null)
        {
            if (dispatcher(opcode, raw, out packet))
            {
                return true;
            }
        }

        return TRY_DESERIALIZE_FALLBACK(opcode, raw, out packet);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool TRY_DESERIALIZE_FALLBACK(ushort opcode, ReadOnlySpan<byte> raw, [NotNullWhen(true)] out IPacket? packet)
        {
            PacketDeserializer?[] table = GetBuilt();
            PacketDeserializer? deserializer = Volatile.Read(ref table[opcode]);

            if (deserializer is null)
            {
                packet = null;
                return false;
            }

            packet = deserializer(raw);
            return packet is not null;
        }
    }

    #endregion APIs

    #region Private Methods

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void THROW_IF_OPCODE_COLLISION(ushort opcode, string name, Dictionary<ushort, string>? names)
    {
        if (names is not null && names.TryGetValue(opcode, out string? oldName) &&
            !StringComparer.Ordinal.Equals(oldName, name))
        {
            throw new InternalErrorException($"[PacketRegistry] OpCode collision! {opcode}: {oldName} vs {name}");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static PacketDeserializer?[] GetBuilt()
    {
        return Volatile.Read(ref s_table)
            ?? throw new InvalidOperationException("PacketRegistry is not built. Call PacketRegistry.Build() after all packet assemblies are loaded.");
    }

    #endregion Private Methods
}

/// <summary>
/// Resolves and deserializes a generated packet without registry dictionary lookup.
/// </summary>
public delegate bool PacketDispatch(ushort opcode, ReadOnlySpan<byte> raw, [NotNullWhen(true)] out IPacket? packet);
