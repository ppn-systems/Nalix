// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Abstractions;
using Nalix.Common.Networking.Packets;
using Nalix.SDK.Extensions;
using Nalix.SDK.Transport.Extensions;

namespace Nalix.SDK.Transport;

/// <summary>
/// Internal helper that encapsulates the recurring boilerplate shared by all
/// "subscribe -> await matching packet -> timeout -> unsubscribe" operations.
/// </summary>
public static class PacketAwaiter
{
    /// <summary>
    /// Subscribes for a matching packet, invokes <paramref name="sendAsync"/>,
    /// and waits until the first packet of type <typeparamref name="TPkt"/> that
    /// satisfies <paramref name="predicate"/> arrives — or the operation times out / is canceled.
    /// </summary>
    /// <typeparam name="TPkt"></typeparam>
    /// <param name="client"></param>
    /// <param name="predicate"></param>
    /// <param name="timeoutMs"></param>
    /// <param name="sendAsync"></param>
    /// <param name="ct"></param>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    /// <exception cref="TimeoutException"></exception>
    /// <exception cref="OperationCanceledException"></exception>
    public static async Task<TPkt> AwaitAsync<TPkt>(
        TransportSession client, Func<TPkt, bool> predicate,
        int timeoutMs, Func<CancellationToken, Task> sendAsync, CancellationToken ct)
        where TPkt : class, IPacket
    {
        // Parameter validation
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(sendAsync);

        if (timeoutMs < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(timeoutMs), "timeoutMs must be >= 0 (0 = infinite)");
        }

        TaskCompletionSource<TPkt> tcs = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        using CancellationTokenSource lcts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        if (timeoutMs > 0)
        {
            lcts.CancelAfter(timeoutMs);
        }

        // Register cancellation -> cancel the TCS. Use 'using' for the registration (not 'await using').
        using CancellationTokenRegistration reg = lcts.Token.Register(() =>
        {
            try { _ = tcs.TrySetCanceled(lcts.Token); } catch { /* swallow */ }
        });

        // Subscribe BEFORE sending — no missed responses regardless of server latency.
        using IDisposable sub = client.SubscribeTemp(OnMessageReceived, OnDisconnected);

        // Delegate to caller for the actual send (e.g. client.SendAsync, SendControlAsync, …)
        try
        {

            await sendAsync(lcts.Token).ConfigureAwait(false);

        }
        catch (Exception sendEx)
        {
            // Ensure awaiting tasks are signalled about the send failure.
            try { _ = tcs.TrySetException(sendEx); } catch { /* swallow */ }

            throw;
        }

        try
        {
            return await tcs.Task.ConfigureAwait(false);
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            throw new OperationCanceledException(ct);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException($"No {typeof(TPkt).Name} received within {timeoutMs} ms.");
        }

        // Local handlers

        void OnMessageReceived(object? _, IBufferLease buffer)
        {
            // Try deserialize — if it fails, let it propagate to the transport.
            IPacket p = client.Catalog.Deserialize(buffer.Span);

            if (p is TPkt match)
            {
                if (predicate(match))
                {
                    // Found a match; try to set the result (may race with cancellation/disconnect).
                    _ = tcs.TrySetResult(match);
                }
            }
        }

        void OnDisconnected(object? _, Exception ex)
        {
            Exception exToSet = ex ?? new InvalidOperationException($"Disconnected while waiting for {typeof(TPkt).Name}.");
            try { _ = tcs.TrySetException(exToSet); } catch { /* swallow */ }

        }
    }
}
