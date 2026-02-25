// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Diagnostics;
using Nalix.Common.Infrastructure.Caching;
using Nalix.Common.Infrastructure.Client;
using Nalix.Common.Messaging.Packets.Abstractions;
using Nalix.Common.Messaging.Protocols;
using Nalix.Framework.Injection;
using Nalix.Framework.Time;
using Nalix.Shared.Extensions;
using Nalix.Shared.Messaging.Controls;            // Control

namespace Nalix.SDK.Transport.Extensions;

/// <summary>
/// Client-side time synchronization for ReliableClient (event-driven).
/// Flow:
/// 1) Capture local mono ticks (t0).
/// 2) SEND Control(opCode, TIME_SYNC, TCP) -> server stamps its Unix ms.
/// 3) Await server Control response with same opCode.
/// 4) Capture local mono ticks (t1), compute RTT.
/// 5) Call Clock.SynchronizeUnixMilliseconds(serverUnixMs, rttMs).
/// </summary>
[System.Runtime.CompilerServices.SkipLocalsInit]
public static class TimeSyncExtensions
{
    /// <summary>
    /// Performs a one-shot time synchronization with the server.
    /// </summary>
    /// <param name="client">RELIABLE client instance.</param>
    /// <param name="opCode">
    /// Operation code to match request/response.
    /// Use a dedicated opcode for time sync (e.g. 2).
    /// </param>
    /// <param name="timeoutMs">Total timeout (send + await).</param>
    /// <param name="maxAllowedDriftMs">
    /// Maximum allowed drift before applying adjustment. Smaller values mean more strict sync.
    /// </param>
    /// <param name="maxHardAdjustMs">
    /// Maximum allowed absolute difference; above this value, adjustment is ignored.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// true if time sync succeeded; false on timeout/failure.
    /// </returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static async System.Threading.Tasks.Task<System.Boolean> TimeSyncAsync(
        this IClient client,
        System.UInt16 opCode = 2,
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

        // Prepare TCS and timeout
        System.Threading.Tasks.TaskCompletionSource<Control> tcs =
            new(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);

        using System.Threading.CancellationTokenSource linked =
            System.Threading.CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(timeoutMs);

        // Temporary listener (auto-removed in finally)
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        void OnPacket(System.Object _, IBufferLease buffer)
        {
            InstanceManager.Instance.GetExistingInstance<IPacketCatalog>().TryDeserialize(buffer.Span, out IPacket p);

            if (p is Control ctrl &&
                ctrl.OpCode == opCode &&
                ctrl.Protocol == ProtocolType.TCP)
            {
                _ = tcs.TrySetResult(ctrl);
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        void OnDisconnected(System.Object _, System.Exception ex)
        {
            _ = tcs.TrySetException(
                ex ?? new System.InvalidOperationException("Disconnected during time sync."));
        }

        using System.IDisposable sub = client.SubscribeTemp(OnPacket, OnDisconnected);

        try
        {
            // Subscribe BEFORE sending to avoid race
            System.Int64 t0Mono = Clock.MonoTicksNow();

            // Build request control packet
            Control req = new();
            req.Initialize(
                opCode: opCode,
                type: ControlType.TIME_SYNC_REQUEST,
                sequenceId: 0,
                reasonCode: ProtocolReason.NONE,
                transport: ProtocolType.TCP);

            await client.SendAsync(req, linked.Token)
                        .ConfigureAwait(false);

            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug("[SDK.TimeSyncAsync] Time sync request sent.");

            using (tcs.LinkCancellation(linked.Token))
            {
                // Wait for server response
                Control resp = await tcs.Task.ConfigureAwait(false);

                // RTT (client mono clock)
                System.Int64 t1Mono = Clock.MonoTicksNow();
                System.Double rttMs = Clock.MonoTicksToMilliseconds(t1Mono - t0Mono);

                // Server wall-clock at send time (Unix ms, filled by server Clock.UnixMillisecondsNow())
                System.Int64 serverUnixMs = resp.Timestamp;

                // Apply synchronization
                System.Double adjustMs = Clock.SynchronizeUnixMilliseconds(
                    serverUnixMs: serverUnixMs, rttMs: rttMs,
                    maxAllowedDriftMs: maxAllowedDriftMs, maxHardAdjustMs: maxHardAdjustMs);

                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Info($"[SDK.TimeSyncAsync] Completed. RTT={rttMs:F2}ms, Adjust={adjustMs:F2}ms.");

                return true;
            }
        }
        catch (System.OperationCanceledException oce)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug($"[SDK.TimeSyncAsync] Canceled: {oce.Message}.");
            return false;
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[SDK.TimeSyncAsync] Failed: {ex}.");
            return false;
        }
    }
}
