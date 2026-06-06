// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking.Packets;

using Nalix.Runtime.Dispatching;
namespace Nalix.Runtime.Internal.Compilation;

internal sealed partial class PacketHandlerCompiler<TController, TPacket>
{
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static PacketMetadata GET_PACKET_METADATA(MethodInfo method)
    {
        return s_attributeCache.GetOrAdd(method, static m =>
        {
            PacketMetadataBuilder builder = new()
            {
                Opcode = CustomAttributeExtensions.GetCustomAttribute<PacketOpcodeAttribute>(m),
                Timeout = CustomAttributeExtensions.GetCustomAttribute<PacketTimeoutAttribute>(m),
                Permission = CustomAttributeExtensions.GetCustomAttribute<PacketPermissionAttribute>(m),
                Encryption = CustomAttributeExtensions.GetCustomAttribute<PacketEncryptionAttribute>(m),
                RateLimit = CustomAttributeExtensions.GetCustomAttribute<PacketRateLimitAttribute>(m),
                ConcurrencyLimit = CustomAttributeExtensions.GetCustomAttribute<PacketConcurrencyLimitAttribute>(m),
                Transport = CustomAttributeExtensions.GetCustomAttribute<PacketTransportAttribute>(m)
            };

            foreach (IPacketMetadataProvider provider in PacketMetadataProviders.Providers)
            {
                provider.Populate(m, builder);
            }

            return builder.Build();
        });
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static string FORMAT_HANDLER_INFO(string x00, ushort x01, MethodInfo? x02 = null, Type? x03 = null)
    {
        string op = $"opcode=0x{x01:X4}";
        string ctrl = $"controller={x00}";
        string m = x02 is null ? "" : $" method={x02.Name}";
        string sig = x02 is null ? "" : $" sig=({string.Join(",", Enumerable
                                                       .Select(x02
                                                       .GetParameters(), p => p.ParameterType.Name))})->{x03?.Name ?? "void"}";

        return $"{op} {ctrl}{m}{sig}";
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PropertyInfo GET_REQUIRED_PROPERTY(Type type, string name)
        => type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
        ?? throw new InternalErrorException($"Required property '{type.FullName}.{name}' was not found.");

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static MethodInfo GET_REQUIRED_METHOD(Type type, string name, BindingFlags bindingFlags)
        => type.GetMethod(name, bindingFlags)
        ?? throw new InternalErrorException($"Required method '{type.FullName}.{name}' was not found.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void THROW_INVALID_SECOND_PARAMETER(MethodInfo method, int length)
    {
        throw new InternalErrorException(
            $"Handler '{method.DeclaringType?.Name}.{method.Name}': " +
            "when the first parameter is PacketContext<T>, " +
            $"the only valid second parameter is CancellationToken. Found {length} parameter(s).");
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void THROW_RAW_MEMORY_MISSING_CONNECTION(MethodInfo method)
    {
        throw new InternalErrorException(
            $"Handler '{method.DeclaringType?.Name}.{method.Name}': " +
            "raw memory signature requires (ReadOnlyMemory<byte>, IConnection[, CancellationToken]). " +
            "Second parameter must implement IConnection.");
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void THROW_RAW_MEMORY_INVALID_PARAM_COUNT(MethodInfo method, int length)
    {
        throw new InternalErrorException(
            $"Handler '{method.DeclaringType?.Name}.{method.Name}': " +
            $"raw memory signature only supports 2 or 3 parameters (ReadOnlyMemory<byte>, IConnection[, CancellationToken]). Found {length}.");
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void THROW_LEGACY_MISSING_CONNECTION(MethodInfo method)
    {
        throw new InternalErrorException(
            $"Handler '{method.DeclaringType?.Name}.{method.Name}': " +
            "legacy signature requires (TPacket, IConnection[, CancellationToken]). " +
            "Second parameter must implement IConnection.");
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void THROW_LEGACY_INVALID_PARAM_COUNT(MethodInfo method, int length)
    {
        throw new InternalErrorException(
            $"Handler '{method.DeclaringType?.Name}.{method.Name}': " +
            $"legacy signature only supports 2 or 3 parameters (TPacket, IConnection[, CancellationToken]). Found {length}.");
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void THROW_UNRECOGNIZED_SIGNATURE(MethodInfo method)
    {
        throw new InternalErrorException(
            $"Handler '{method.DeclaringType?.Name}.{method.Name}': " +
            "unrecognised signature. " +
            "Supported forms: " +
            "(TPacket, IConnection), " +
            "(TPacket, IConnection, CancellationToken), " +
            "(TConcretePacket, IConnection), " +
            "(TConcretePacket, IConnection, CancellationToken), " +
            "(PacketContext<T>), " +
            "(PacketContext<T>, CancellationToken), " +
            "(ReadOnlyMemory<byte>, IConnection), " +
            "(ReadOnlyMemory<byte>, IConnection, CancellationToken).");
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void THROW_KIND_OUT_OF_RANGE(SignatureKind kind) => throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
}
