// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Networking.Transport;
using Nalix.SDK.Diagnostics;
using Nalix.SDK.Transport;

namespace Nalix.SDK.Extensions;

/// <summary>
/// Extension method that captures a <see cref="ReliableClientDiagnostics"/> from a
/// <see cref="ReliableClient"/> in a single call.
/// </summary>
public static class DiagnosticsExtensions
{
    /// <summary>
    /// Captures an immutable diagnostics snapshot of the client's current metrics.
    /// </summary>
    /// <param name="client">The client to snapshot.</param>
    /// <returns>
    /// A <see cref="ReliableClientDiagnostics"/> with all metrics captured at this instant.
    /// </returns>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="client"/> is <c>null</c>.
    /// </exception>
    /// <example>
    /// <code>
    /// var snap = client.GetDiagnostics();
    /// logger.Info(snap.ToString());
    ///
    /// // Or access individual fields:
    /// if (snap.HeartbeatRttMs > 200)
    ///     logger.Warn($"High RTT: {snap.HeartbeatRttMs:F1} ms");
    /// </code>
    /// </example>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static ReliableClientDiagnostics GetDiagnostics(this ReliableClient client)
    {
        System.ArgumentNullException.ThrowIfNull(client);

        return new ReliableClientDiagnostics
        {
            IsConnected = client.IsConnected,
            Endpoint = $"{client.Options.Address}:{client.Options.Port}",
            TotalBytesSent = client.BytesSent,
            TotalBytesReceived = client.BytesReceived,
            SendBytesPerSecond = client.SendBytesPerSecond,
            ReceiveBytesPerSecond = client.ReceiveBytesPerSecond,
            CapturedAt = System.DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Captures a diagnostics snapshot via the <see cref="IClientConnection"/> interface.
    /// Only <see cref="ReliableClient"/> instances expose the full metric set;
    /// other implementations receive a partial snapshot (RTT = 0, BPS = 0).
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static ReliableClientDiagnostics GetDiagnostics(this IClientConnection client)
    {
        System.ArgumentNullException.ThrowIfNull(client);

        // Fast path: full metrics available.
        if (client is ReliableClient rc)
        {
            return rc.GetDiagnostics();
        }

        // Partial snapshot for other IClientConnection implementations.
        return new ReliableClientDiagnostics
        {
            IsConnected = client.IsConnected,
            Endpoint = $"{client.Options.Address}:{client.Options.Port}",
            CapturedAt = System.DateTime.UtcNow,
        };
    }
}