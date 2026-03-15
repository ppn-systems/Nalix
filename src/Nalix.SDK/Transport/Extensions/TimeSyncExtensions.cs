// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Diagnostics.Abstractions;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Networking.Transport;
using Nalix.Framework.Injection;
using Nalix.Framework.Time;
using Nalix.Shared.Frames.Controls;

namespace Nalix.SDK.Transport.Extensions;

/// <summary>
/// Client-side time synchronization extension for <see cref="IClientConnection"/> (event-driven).
/// </summary>
/// <remarks>
/// Flow:
/// <list type="number">
/// <item>Capture local monotonic ticks <c>t0</c>.</item>
/// <item>SEND <see cref="ControlType.TIME_SYNC_REQUEST"/> control frame.</item>
/// <item>Await server <see cref="Control"/> response with the same opCode.</item>
/// <item>Capture <c>t1</c>, compute RTT = <c>t1 - t0</c>.</item>
/// <item>Call <see cref="Clock.SynchronizeUnixMilliseconds"/> with the server timestamp and RTT.</item>
/// </list>
/// </remarks>
[System.Runtime.CompilerServices.SkipLocalsInit]
public static class TimeSyncExtensions
{
    // Lazy logger resolution: avoids hard startup failure if ILogger is registered after this type loads.
    private static ILogger Log => InstanceManager.Instance.GetExistingInstance<ILogger>();

    /// <summary>
    /// Performs a one-shot time synchronization with the server.
    /// </summary>
    /// <param name="client">The connected reliable client.</param>
    /// <param name="opCode">
    /// Operation code used to correlate request/response.
    /// Use a dedicated opcode for time sync (e.g. <c>2</c>).
    /// </param>
    /// <param name="sequenceId">Optional sequence identifier. Default is <c>0</c>.</param>
    /// <param name="timeoutMs">Total timeout in milliseconds (send + await). Default is 2 000 ms.</param>
    /// <param name="maxAllowedDriftMs">
    /// Maximum drift in milliseconds before an adjustment is applied.
    /// Smaller values enforce stricter synchronization.
    /// </param>
    /// <param name="maxHardAdjustMs">
    /// Maximum absolute adjustment in milliseconds.
    /// Adjustments above this threshold are discarded as likely invalid.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> if synchronization succeeded; <c>false</c> on timeout or failure.</returns>
    public static async System.Threading.Tasks.Task<System.Boolean> TimeSyncAsync(
        this IClientConnection client,
        System.UInt16 opCode = 2,
        System.UInt32 sequenceId = 0,
        System.Int32 timeoutMs = -1,
        System.Double maxAllowedDriftMs = 1_000.0,
        System.Double maxHardAdjustMs = 10_000.0,
        System.Threading.CancellationToken ct = default)
    {
        System.ArgumentNullException.ThrowIfNull(client);

        if (!client.IsConnected)
        {
            return false;
        }

        System.Int32 effectiveTimeout = timeoutMs > 0
            ? timeoutMs
            : System.Math.Max(1000, client.Options.KeepAliveIntervalMillis / 2);

        // Stamp t0 before building the request — as early as possible for accuracy.
        System.Int64 t0Mono = Clock.MonoTicksNow();

        Control req = new();
        req.Initialize(
            opCode: opCode,
            type: ControlType.TIME_SYNC_REQUEST,
            sequenceId: sequenceId,
            reasonCode: ProtocolReason.NONE,
            transport: ProtocolType.TCP);

        Log?.Debug("[SDK.TimeSyncAsync] Sending time sync request.");

        try
        {
            // RequestAsync: subscribe → send → await matching Control in one call.
            Control resp = await client.RequestAsync<Control, Control>(
                req,
                predicate: p => p.OpCode == opCode && p.Protocol == ProtocolType.TCP,
                timeoutMs: effectiveTimeout,
                ct: ct).ConfigureAwait(false);

            System.Int64 t1Mono = Clock.MonoTicksNow();
            System.Double rttMs = Clock.MonoTicksToMilliseconds(t1Mono - t0Mono);
            System.Int64 serverMs = resp.Timestamp;

            System.Double adjustMs = Clock.SynchronizeUnixMilliseconds(
                serverUnixMs: serverMs,
                rttMs: rttMs,
                maxAllowedDriftMs: maxAllowedDriftMs,
                maxHardAdjustMs: maxHardAdjustMs);

            Log?.Info($"[SDK.TimeSyncAsync] Completed. RTT={rttMs:F2} ms, Adjust={adjustMs:F2} ms.");
            return true;
        }
        catch (System.OperationCanceledException oce)
        {
            Log?.Debug($"[SDK.TimeSyncAsync] Canceled: {oce.Message}.");
            return false;
        }
        catch (System.Exception ex)
        {
            Log?.Error($"[SDK.TimeSyncAsync] Failed: {ex}.");
            return false;
        }
    }
}