// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Nalix.Common.Networking.Packets;

namespace Nalix.Network.Routing.Metadata;

/// <summary>
/// Represents a fully attributed packet descriptor used to define behavior and metadata
/// of network packets, such as operation code, timeout policy, rate limits, permission requirements,
/// and encryption strategy.
/// </summary>
/// <param name="opCode"></param>
/// <param name="timeout"></param>
/// <param name="permission"></param>
/// <param name="encryption"></param>
/// <param name="rateLimit"></param>
/// <param name="concurrencyLimit"></param>
/// <param name="customAttributes"></param>
/// <remarks>
/// This struct uses sequential layout and is optimized for performance in network dispatch systems.
/// All attributes are immutable for safe usage in high-throughput scenarios.
/// </remarks>
[StructLayout(
    LayoutKind.Sequential, Pack = 1)]
[method: MethodImpl(
    MethodImplOptions.AggressiveInlining)]
[EditorBrowsable(EditorBrowsableState.Never)]
public readonly struct PacketMetadata(
    [NotNull] PacketOpcodeAttribute opCode,
    [AllowNull] PacketTimeoutAttribute timeout,
    [AllowNull] PacketPermissionAttribute permission,
    [AllowNull] PacketEncryptionAttribute encryption,
    [AllowNull] PacketRateLimitAttribute rateLimit,
    [AllowNull] PacketConcurrencyLimitAttribute concurrencyLimit,
    IReadOnlyDictionary<Type, Attribute> customAttributes = null)
{
    /// <summary>
    /// Gets the operation code attribute which uniquely identifies the type of packet.
    /// </summary>
    public readonly PacketOpcodeAttribute PacketOpcode = opCode;

    /// <summary>
    /// Gets the optional timeout attribute which defines the time duration
    /// after which the packet operation is considered expired.
    /// </summary>
    public readonly PacketTimeoutAttribute Timeout = timeout;

    /// <summary>
    /// Gets the optional permission attribute that specifies access control
    /// or authorization level required to handle this packet.
    /// </summary>
    public readonly PacketPermissionAttribute Permission = permission;

    /// <summary>
    /// Gets the optional encryption attribute that defines the required
    /// encryption mechanism for this packet’s payload.
    /// </summary>
    public readonly PacketEncryptionAttribute Encryption = encryption;

    /// <summary>
    /// Gets the optional rate limit attribute that specifies the allowed burst and
    /// requests per second for this packet, used to control network traffic and prevent abuse.
    /// </summary>
    public readonly PacketRateLimitAttribute RateLimit = rateLimit;

    /// <summary>
    /// Gets the optional concurrency limit attribute that specifies the maximum number of concurrent
    /// operations allowed for this packet, and optionally the queuing behavior if the limit is reached.
    /// </summary>
    public readonly PacketConcurrencyLimitAttribute ConcurrencyLimit = concurrencyLimit;

    /// <summary>
    /// Gets a read-only dictionary of custom metadata attributes,
    /// keyed by their concrete <see cref="Type"/>.
    /// </summary>
    public readonly IReadOnlyDictionary<Type, Attribute> CustomAttributes = customAttributes
        ?? new Dictionary<Type, Attribute>();

    /// <summary>
    /// Gets a custom attribute of the specified type if it exists.
    /// </summary>
    /// <typeparam name="TAttribute">The attribute type to retrieve.</typeparam>
    /// <returns>
    /// The attribute instance if it exists; otherwise, <see langword="null"/>.
    /// </returns>
    public TAttribute GetCustomAttribute<TAttribute>() where TAttribute : Attribute
        => CustomAttributes.TryGetValue(typeof(TAttribute), out Attribute value) ? (TAttribute)value : null;
}
