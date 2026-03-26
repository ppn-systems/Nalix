// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Nalix.Common.Networking.Transport;
using Nalix.SDK.Transport;

namespace Nalix.SDK.Extensions;

/// <summary>
/// Extension method that captures a <see cref="TcpSessionDiagnostics"/> from a
/// <see cref="TcpSession"/> in a single call.
/// </summary>
public static class DiagnosticsExtensions
{
    /// <summary>
    /// Captures an immutable diagnostics snapshot of the client's current metrics.
    /// </summary>
    /// <param name="client">The client to snapshot.</param>
    /// <returns>
    /// A <see cref="TcpSessionDiagnostics"/> with all metrics captured at this instant.
    /// </returns>
    /// <exception cref="ArgumentNullException">
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TcpSessionDiagnostics GetDiagnostics(this TcpSession client)
    {
        ArgumentNullException.ThrowIfNull(client);

        return new TcpSessionDiagnostics
        {
            IsConnected = client.IsConnected,
            Endpoint = $"{client.Options.Address}:{client.Options.Port}",
            TotalBytesSent = client.BytesSent,
            TotalBytesReceived = client.BytesReceived,
            SendBytesPerSecond = client.SendBytesPerSecond,
            ReceiveBytesPerSecond = client.ReceiveBytesPerSecond,
            CapturedAt = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Captures a diagnostics snapshot via the <see cref="IClientConnection"/> interface.
    /// Only <see cref="TcpSession"/> instances expose the full metric set;
    /// other implementations receive a partial snapshot (RTT = 0, BPS = 0).
    /// </summary>
    /// <param name="client"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TcpSessionDiagnostics GetDiagnostics(this IClientConnection client)
    {
        ArgumentNullException.ThrowIfNull(client);

        // Fast path: full metrics available.
        if (client is TcpSession rc)
        {
            return rc.GetDiagnostics();
        }

        // Partial snapshot for other IClientConnection implementations.
        return new TcpSessionDiagnostics
        {
            IsConnected = client.IsConnected,
            Endpoint = $"{client.Options.Address}:{client.Options.Port}",
            CapturedAt = DateTime.UtcNow,
        };
    }
}

/// <summary>
/// An immutable point-in-time snapshot of <see cref="TcpSession"/> metrics.
/// All values are captured atomically from the client at the moment
/// <see cref="DiagnosticsExtensions.GetDiagnostics(TcpSession)"/> is called.
/// </summary>
[DebuggerDisplay(
    "Connected={IsConnected}, RTT={HeartbeatRttMs:F1} ms, " +
    "Tx={SendBytesPerSecond} B/s, Rx={ReceiveBytesPerSecond} B/s")]
public readonly struct TcpSessionDiagnostics
{
    /// <summary>
    /// Whether the client is currently connected.
    /// </summary>
    public bool IsConnected { get; init; }

    /// <summary>
    /// Endpoint the client is connected to (or last attempted).
    /// </summary>
    public string Endpoint { get; init; }

    /// <summary>
    /// Total bytes sent since the last <c>ConnectAsync</c>.
    /// </summary>
    public long TotalBytesSent { get; init; }

    /// <summary>
    /// Total bytes received since the last <c>ConnectAsync</c>.
    /// </summary>
    public long TotalBytesReceived { get; init; }

    /// <summary>
    /// Send throughput in bytes/second over the last sample interval (~1 s).
    /// </summary>
    public long SendBytesPerSecond { get; init; }

    /// <summary>
    /// Receive throughput in bytes/second over the last sample interval (~1 s).
    /// </summary>
    public long ReceiveBytesPerSecond { get; init; }

    /// <summary>
    /// UTC time when the snapshot was taken.
    /// </summary>
    public DateTime CapturedAt { get; init; }

    /// <summary>
    /// Returns a human-readable summary of the snapshot suitable for logging.
    /// </summary>
    public override string ToString()
        => $"[Diagnostics @ {this.CapturedAt:HH:mm:ss.fff}] " +
           $"Connected={this.IsConnected} Endpoint={this.Endpoint} " +
           $"Sent={this.TotalBytesSent:N0} B Recv={this.TotalBytesReceived:N0} B " +
           $"TxBps={this.SendBytesPerSecond:N0} RxBps={this.ReceiveBytesPerSecond:N0} ";
}
