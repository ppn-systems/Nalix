// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Diagnostics.Abstractions;
using Nalix.Common.Networking.Caching;
using Nalix.Common.Networking.Packets.Abstractions;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Networking.Transport;
using Nalix.Framework.Injection;
using Nalix.Framework.Time;
using Nalix.Shared.Extensions;
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
        System.Int32 timeoutMs = 2_000,
        System.Double maxAllowedDriftMs = 1_000.0,
        System.Double maxHardAdjustMs = 10_000.0,
        System.Threading.CancellationToken ct = default)
    {
        System.ArgumentNullException.ThrowIfNull(client);

        if (!client.IsConnected)
        {
            return false;
        }

        IPacketCatalog catalog = InstanceManager.Instance.GetExistingInstance<IPacketCatalog>();

        System.Threading.Tasks.TaskCompletionSource<Control> tcs =
            new(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);

        using System.Threading.CancellationTokenSource linked =
            System.Threading.CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(timeoutMs);

        // Subscribe BEFORE sending to eliminate the race where the server responds before we listen.
        using System.IDisposable sub = client.SubscribeTemp(OnPacket, OnDisconnected);

        try
        {
            // Stamp t0 after subscribing but before sending.
            System.Int64 t0Mono = Clock.MonoTicksNow();

            Control req = new();
            req.Initialize(
                opCode: opCode,
                type: ControlType.TIME_SYNC_REQUEST,
                sequenceId: sequenceId,
                reasonCode: ProtocolReason.NONE,
                transport: ProtocolType.TCP);

            await client.SendAsync(req, linked.Token).ConfigureAwait(false);

            Log?.Debug("[SDK.TimeSyncAsync] Time sync request sent.");

            using (tcs.LinkCancellation(linked.Token))
            {
                Control resp = await tcs.Task.ConfigureAwait(false);

                System.Int64 t1Mono = Clock.MonoTicksNow();
                System.Double rttMs = Clock.MonoTicksToMilliseconds(t1Mono - t0Mono);
                System.Int64 serverUnixMs = resp.Timestamp;

                System.Double adjustMs = Clock.SynchronizeUnixMilliseconds(
                    serverUnixMs: serverUnixMs,
                    rttMs: rttMs,
                    maxAllowedDriftMs: maxAllowedDriftMs,
                    maxHardAdjustMs: maxHardAdjustMs);

                Log?.Info($"[SDK.TimeSyncAsync] Completed. RTT={rttMs:F2} ms, Adjust={adjustMs:F2} ms.");
                return true;
            }
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

        void OnPacket(System.Object _, IBufferLease buffer)
        {
            // Always dispose the lease; deserialize takes a ReadOnlySpan copy.
            using (buffer)
            {
                if (!catalog.TryDeserialize(buffer.Span, out IPacket p))
                {
                    return;
                }

                if (p is Control ctrl &&
                    ctrl.OpCode == opCode &&
                    ctrl.Protocol == ProtocolType.TCP)
                {
                    tcs.TrySetResult(ctrl);
                }
            }
        }

        void OnDisconnected(System.Object _, System.Exception ex)
            => tcs.TrySetException(ex ?? new System.InvalidOperationException("Disconnected during time sync."));
    }
}