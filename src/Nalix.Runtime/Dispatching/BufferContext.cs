// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using Nalix.Abstractions.Networking;

namespace Nalix.Runtime.Dispatching;

/// <summary>
/// Lightweight value type that bundles raw packet data, connection, and cancellation token
/// for handlers that receive wire bytes directly instead of a deserialized packet.
/// This is the raw-handler counterpart of <see cref="PacketContext{TPacket}"/>.
/// </summary>
/// <param name="rawData">The raw packet bytes including header and payload.</param>
/// <param name="connection">The connection from which the packet was received.</param>
/// <param name="isReliable">Whether the packet was received over a reliable transport (TCP).</param>
/// <param name="cancellationToken">The cancellation token for this dispatch.</param>
[SuppressMessage("Design", "CA1051:Do not declare visible instance fields", Justification = "Performance-critical value type; fields match PacketHeader convention.")]
[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
public readonly struct BufferContext(
    ReadOnlyMemory<byte> rawData,
    IConnection connection,
    bool isReliable,
    CancellationToken cancellationToken)
{
    /// <summary>
    /// Gets the raw packet data including header and payload.
    /// </summary>
    public readonly ReadOnlyMemory<byte> RawData = rawData;

    /// <summary>
    /// Gets the connection associated with this packet.
    /// </summary>
    public readonly IConnection Connection = connection;

    /// <summary>
    /// Gets a value indicating whether the transport is reliable (TCP).
    /// </summary>
    public readonly bool IsReliable = isReliable;

    /// <summary>
    /// Gets the cancellation token for this dispatch.
    /// </summary>
    public readonly CancellationToken CancellationToken = cancellationToken;
}
