// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Networking.Protocols;
using Nalix.Framework.Time;

namespace Nalix.SDK.Transport.Extensions;

/// <summary>
/// Provides extension methods for <see cref="TcpSession"/> for bandwidth sampling and heartbeat operations.
/// </summary>
public static class MonitoringExtensions
{
    /// <summary>
    /// Samples byte counters at each interval and updates the last bytes-per-second (BPS) readings for the client.
    /// </summary>
    /// <param name="client">The <see cref="TcpSession"/> instance to monitor.</param>
    /// <param name="token">The <see cref="System.Threading.CancellationToken"/> that can be used to cancel the operation.</param>
    /// <returns>
    /// A completed <see cref="System.Threading.Tasks.Task"/> representing the asynchronous operation.
    /// </returns>
    /// <remarks>
    /// Đo bandwidth gửi/nhận của client mỗi interval.
    /// </remarks>
    public static System.Threading.Tasks.Task RateSamplerTickAsync(
        this TcpSession client, System.Threading.CancellationToken token)
    {
        if (token.IsCancellationRequested)
        {
            return System.Threading.Tasks.Task.CompletedTask;
        }

        // Get the current tick time
        System.Int64 now = Clock.MonoTicksNow();
        System.Double elapsedSec = Clock.MonoTicksToMilliseconds((now - client._lastSampleTick) ?? 0) / 1000.0;

        client._lastSampleTick = now;

        try
        {
            System.Int64 sent = System.Threading.Interlocked.Exchange(ref client._sendCounterForInterval, 0);
            System.Int64 recv = System.Threading.Interlocked.Exchange(ref client._receiveCounterForInterval, 0);

            System.Threading.Interlocked.Exchange(ref client._lastSendBps, (System.Int64)(sent / elapsedSec));
            System.Threading.Interlocked.Exchange(ref client._lastReceiveBps, recv);
        }
        catch (System.Exception ex)
        {
            TcpSession.s_log?.Warn($"[SDK.{nameof(TcpSession)}.{nameof(RateSamplerTickAsync)}] sampler-error: {ex.Message}");
        }

        return System.Threading.Tasks.Task.CompletedTask;
    }

    /// <summary>
    /// Executes a fallback heartbeat loop that sends a control ping at a configured interval until cancellation is requested.
    /// </summary>
    /// <param name="client">The <see cref="TcpSession"/> instance.</param>
    /// <param name="token">A <see cref="System.Threading.CancellationToken"/> to observe while waiting for the task to complete.</param>
    /// <returns>
    /// A <see cref="System.Threading.Tasks.Task"/> representing the asynchronous operation.
    /// </returns>
    /// <remarks>
    /// Gửi heartbeat (PING control) định kỳ — gọi từ client.
    /// </remarks>
    /// <exception cref="System.OperationCanceledException">
    /// Thrown if the operation is canceled by disconnect or object disposal.
    /// </exception>
    public static async System.Threading.Tasks.Task HeartbeatLoopAsync(
        this TcpSession client, System.Threading.CancellationToken token)
    {
        System.Int32 intervalMs = System.Math.Max(1, client.Options.KeepAliveIntervalMillis);

        while (!token.IsCancellationRequested)
        {
            try
            {
                await System.Threading.Tasks.Task.Delay(intervalMs, token).ConfigureAwait(false);

                // Send CONTROL PING as heartbeat.
                await client.SendControlAsync(
                    opCode: 0,
                    type: ControlType.PING,
                    configure: ctrl =>
                    {
                        ctrl.SequenceId = Nalix.Framework.Random.Csprng.NextUInt32();
                        ctrl.Protocol = Nalix.Common.Networking.Protocols.ProtocolType.TCP;
                        ctrl.MonoTicks = Clock.MonoTicksNow();
                        ctrl.Timestamp = Clock.UnixMillisecondsNow();
                    },
                    ct: token
                ).ConfigureAwait(false);
            }
            catch (System.OperationCanceledException)
            {
                break; // Canceled by disconnect/dispose.
            }
            catch (System.Exception ex)
            {
                TcpSession.s_log?.Warn($"[SDK.{nameof(TcpSession)}.{nameof(HeartbeatLoopAsync)}] heartbeat-error: {ex.Message}");

                _ = client.HANDLE_DISCONNECT_AND_RECONNECT_ASYNC(ex);
                break;
            }
        }
    }
}