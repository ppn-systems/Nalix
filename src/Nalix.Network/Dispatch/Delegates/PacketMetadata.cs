// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Packets.Attributes;

namespace Nalix.Network.Dispatch.Delegates;

/// <summary>
/// Represents a fully attributed packet descriptor used to define behavior and metadata
/// of network packets, such as operation code, timeout policy, rate limits, permission requirements,
/// and encryption strategy.
/// </summary>
/// <remarks>
/// This struct uses sequential layout and is optimized for performance in network dispatch systems.
/// All attributes are immutable for safe usage in high-throughput scenarios.
/// </remarks>
[System.Runtime.InteropServices.StructLayout(
    System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)]
[method: System.Runtime.CompilerServices.MethodImpl(
    System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
public readonly struct PacketMetadata(
    [System.Diagnostics.CodeAnalysis.DisallowNull] PacketOpcodeAttribute opCode,
    [System.Diagnostics.CodeAnalysis.AllowNull] PacketTimeoutAttribute timeout,
    [System.Diagnostics.CodeAnalysis.AllowNull] PacketPermissionAttribute permission,
    [System.Diagnostics.CodeAnalysis.AllowNull] PacketEncryptionAttribute encryption,
    [System.Diagnostics.CodeAnalysis.AllowNull] PacketRateLimitAttribute rateLimit,
    [System.Diagnostics.CodeAnalysis.AllowNull] PacketConcurrencyLimitAttribute concurrencyLimit)
{
    /// <summary>
    /// Gets the operation code attribute which uniquely identifies the type of packet.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.DisallowNull]
    public readonly PacketOpcodeAttribute OpCode = opCode;

    /// <summary>
    /// Gets the optional timeout attribute which defines the time duration
    /// after which the packet operation is considered expired.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.AllowNull]
    public readonly PacketTimeoutAttribute Timeout = timeout;

    /// <summary>
    /// Gets the optional permission attribute that specifies access control
    /// or authorization level required to handle this packet.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.AllowNull]
    public readonly PacketPermissionAttribute Permission = permission;

    /// <summary>
    /// Gets the optional encryption attribute that defines the required
    /// encryption mechanism for this packet’s payload.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.AllowNull]
    public readonly PacketEncryptionAttribute Encryption = encryption;

    /// <summary>
    /// Gets the optional rate limit attribute that specifies the allowed burst and
    /// requests per second for this packet, used to control network traffic and prevent abuse.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.AllowNull]
    public readonly PacketRateLimitAttribute RateLimit = rateLimit;

    /// <summary>
    /// Gets the optional concurrency limit attribute that specifies the maximum number of concurrent
    /// operations allowed for this packet, and optionally the queuing behavior if the limit is reached.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.AllowNull]
    public readonly PacketConcurrencyLimitAttribute ConcurrencyLimit = concurrencyLimit;
}
