// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;

using Nalix.Runtime.Dispatching;
namespace Nalix.Runtime.Internal.Compilation;

internal sealed partial class PacketHandlerCompiler<TController, TPacket>
{
    /// <summary>
    /// Describes the recognized parameter signature of a handler method.
    /// </summary>
    private enum SignatureKind
    {
        /// <summary>
        /// (TPacket, IConnection)
        /// </summary>
        LegacyNoToken = 0,

        /// <summary>
        /// (TPacket, IConnection, CancellationToken)
        /// </summary>
        LegacyWithToken = 1,

        /// <summary>
        /// (PacketContext&lt;TPacket&gt;)
        /// </summary>
        ContextOnly = 2,

        /// <summary>
        /// (PacketContext&lt;TPacket&gt;, CancellationToken)
        /// </summary>
        ContextWithToken = 3,

        /// <summary>
        /// (TConcretePacket, IConnection) where TConcretePacket : IPacket and TConcretePacket != TPacket.
        /// The dispatcher will perform a runtime type-check and cast before invoking.
        /// </summary>
        LegacyConcreteNoToken = 4,

        /// <summary>
        /// (TConcretePacket, IConnection, CancellationToken) where TConcretePacket : IPacket and TConcretePacket != TPacket.
        /// The dispatcher will perform a runtime type-check and cast before invoking.
        /// </summary>
        LegacyConcreteWithToken = 5,

        /// <summary>
        /// (ReadOnlyMemory&lt;byte&gt;, IConnection)
        /// Extracts raw memory payload from RawMemoryPacket.
        /// </summary>
        MemoryNoToken = 6,

        /// <summary>
        /// (ReadOnlyMemory&lt;byte&gt;, IConnection, CancellationToken)
        /// Extracts raw memory payload from RawMemoryPacket.
        /// </summary>
        MemoryWithToken = 7,
    }

    /// <summary>
    /// Determines the <see cref="SignatureKind"/> of a handler method.
    /// Throws <see cref="InternalErrorException"/> for unrecognised signatures.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SignatureKind RESOLVE_SIGNATURE_KIND(MethodInfo method, ParameterInfo[] parms)
    {
        // ---- new-style: first param is PacketContext<T> for any T : IPacket ----
        if (parms.Length >= 1 && IS_PACKET_CONTEXT_TYPE(parms[0].ParameterType))
        {
            if (parms.Length == 1)
            {
                return SignatureKind.ContextOnly;
            }

            if (parms.Length == 2 && parms[1].ParameterType == typeof(CancellationToken))
            {
                return SignatureKind.ContextWithToken;
            }

            THROW_INVALID_SECOND_PARAMETER(method, parms.Length);
            return default;
        }

        // ---- new-style: raw memory payload ----
        if (parms.Length >= 1 && parms[0].ParameterType == typeof(ReadOnlyMemory<byte>))
        {
            if (parms.Length < 2 || !typeof(IConnection).IsAssignableFrom(parms[1].ParameterType))
            {
                THROW_RAW_MEMORY_MISSING_CONNECTION(method);
                return default;
            }

            if (parms.Length == 2)
            {
                return SignatureKind.MemoryNoToken;
            }

            if (parms.Length == 3 && parms[2].ParameterType == typeof(CancellationToken))
            {
                return SignatureKind.MemoryWithToken;
            }

            THROW_RAW_MEMORY_INVALID_PARAM_COUNT(method, parms.Length);
            return default;
        }

        // ---- legacy-style: first param must implement IPacket ----
        if (parms.Length >= 1 && typeof(IPacket).IsAssignableFrom(parms[0].ParameterType))
        {
            if (parms.Length < 2 || !typeof(IConnection).IsAssignableFrom(parms[1].ParameterType))
            {
                THROW_LEGACY_MISSING_CONNECTION(method);
                return default;
            }

            bool isConcrete = parms[0].ParameterType != typeof(TPacket)
                && typeof(IPacket).IsAssignableFrom(parms[0].ParameterType);

            if (parms.Length == 2)
            {
                return isConcrete
                    ? SignatureKind.LegacyConcreteNoToken
                    : SignatureKind.LegacyNoToken;
            }

            if (parms.Length == 3 && parms[2].ParameterType == typeof(CancellationToken))
            {
                return isConcrete
                    ? SignatureKind.LegacyConcreteWithToken
                    : SignatureKind.LegacyWithToken;
            }

            THROW_LEGACY_INVALID_PARAM_COUNT(method, parms.Length);
            return default;
        }

        THROW_UNRECOGNIZED_SIGNATURE(method);
        return default;
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="type"/> is a closed generic
    /// constructed from <see cref="PacketContext{TPacket}"/>, regardless of which
    /// concrete type argument was supplied.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IS_PACKET_CONTEXT_TYPE(Type type)
        => type.IsGenericType
        && (type.GetGenericTypeDefinition() == typeof(PacketContext<>) || type.GetGenericTypeDefinition() == typeof(IPacketContext<>));
}
