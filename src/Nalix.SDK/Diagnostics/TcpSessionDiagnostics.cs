// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.SDK.Extensions;
using Nalix.SDK.Transport;

namespace Nalix.SDK.Diagnostics;

/// <summary>
/// An immutable point-in-time snapshot of <see cref="TcpSession"/> metrics.
/// All values are captured atomically from the client at the moment
/// <see cref="DiagnosticsExtensions.GetDiagnostics(TcpSession)"/> is called.
/// </summary>
[System.Diagnostics.DebuggerDisplay(
    "Connected={IsConnected}, RTT={HeartbeatRttMs:F1} ms, " +
    "Tx={SendBytesPerSecond} B/s, Rx={ReceiveBytesPerSecond} B/s")]
public readonly struct TcpSessionDiagnostics
{
    /// <summary>
    /// Whether the client is currently connected.
    /// </summary>
    public System.Boolean IsConnected { get; init; }

    /// <summary>
    /// Endpoint the client is connected to (or last attempted).
    /// </summary>
    public System.String Endpoint { get; init; }

    /// <summary>
    /// Total bytes sent since the last <c>ConnectAsync</c>.
    /// </summary>
    public System.Int64 TotalBytesSent { get; init; }

    /// <summary>
    /// Total bytes received since the last <c>ConnectAsync</c>.
    /// </summary>
    public System.Int64 TotalBytesReceived { get; init; }

    /// <summary>
    /// Send throughput in bytes/second over the last sample interval (~1 s).
    /// </summary>
    public System.Int64 SendBytesPerSecond { get; init; }

    /// <summary>
    /// Receive throughput in bytes/second over the last sample interval (~1 s).
    /// </summary>
    public System.Int64 ReceiveBytesPerSecond { get; init; }

    /// <summary>
    /// UTC time when the snapshot was taken.
    /// </summary>
    public System.DateTime CapturedAt { get; init; }

    /// <summary>
    /// Returns a human-readable summary of the snapshot suitable for logging.
    /// </summary>
    public override System.String ToString()
        => $"[Diagnostics @ {CapturedAt:HH:mm:ss.fff}] " +
           $"Connected={IsConnected} Endpoint={Endpoint} " +
           $"Sent={TotalBytesSent:N0} B Recv={TotalBytesReceived:N0} B " +
           $"TxBps={SendBytesPerSecond:N0} RxBps={ReceiveBytesPerSecond:N0} ";
}