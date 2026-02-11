// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Abstractions;
using Nalix.Common.Diagnostics;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.Injection;
using Nalix.SDK.Extensions;
using Nalix.SDK.Transport.Extensions;

namespace Nalix.SDK.Transport.Internal;

/// <summary>
/// Internal helper that encapsulates the recurring boilerplate shared by all
/// "subscribe → await matching packet → timeout → unsubscribe" operations.
/// </summary>
internal static class PACKET_AWAITER
{
    private static readonly ILogger? s_logger = InstanceManager.Instance.GetExistingInstance<ILogger>();

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
    internal static async Task<TPkt> AwaitAsync<TPkt>(
        IClientConnection client,
        Func<TPkt, bool> predicate,
        int timeoutMs,
        Func<CancellationToken, Task> sendAsync,
        CancellationToken ct)
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

        s_logger?.Trace($"[SDK.{nameof(PACKET_AWAITER)}] Subscribing for {typeof(TPkt).Name} (timeout={timeoutMs}ms).");

        // Subscribe BEFORE sending — no missed responses regardless of server latency.
        using IDisposable sub = client.SubscribeTemp(OnMessageReceived, OnDisconnected);

        // Delegate to caller for the actual send (e.g. client.SendAsync, SendControlAsync, …)
        try
        {
            s_logger?.Debug($"[SDK.{nameof(PACKET_AWAITER)}] Invoking send delegate for expected {typeof(TPkt).Name}.");
            await sendAsync(lcts.Token).ConfigureAwait(false);
            s_logger?.Trace($"[SDK.{nameof(PACKET_AWAITER)}] send delegate completed for {typeof(TPkt).Name}.");
        }
        catch (Exception sendEx)
        {
            // Ensure awaiting tasks are signalled about the send failure.
            try { _ = tcs.TrySetException(sendEx); } catch { /* swallow */ }
            s_logger?.Error($"[SDK.{nameof(PACKET_AWAITER)}] send delegate threw: {sendEx.Message}", sendEx);
            throw;
        }

        try
        {
            return await tcs.Task.ConfigureAwait(false);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            s_logger?.Debug($"[SDK.{nameof(PACKET_AWAITER)}] Timeout waiting for {typeof(TPkt).Name} after {timeoutMs}ms.");
            throw new TimeoutException($"No {typeof(TPkt).Name} received within {timeoutMs} ms.");
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            s_logger?.Debug($"[SDK.{nameof(PACKET_AWAITER)}] Operation cancelled by caller while waiting for {typeof(TPkt).Name}.");
            throw new OperationCanceledException(ct);
        }

        // Local handlers

        void OnMessageReceived(object? _, IBufferLease buffer)
        {
            // Ownership: caller of SubscribeTemp provides an IBufferLease; dispose after use.
            try
            {
                // Try deserialize — protect from codec exceptions.
                IPacket p;
                try
                {
                    client.Catalog.Deserialize(buffer.Span, out p);
                }
                catch (Exception dex)
                {
                    s_logger?.Error($"[SDK.{nameof(PACKET_AWAITER)}] Deserialization error while awaiting {typeof(TPkt).Name}: {dex.Message}", dex);
                    // If deserialization fails repeatedly, we do not cancel the whole await — just ignore this buffer.
                    return;
                }

                if (p is TPkt match)
                {
                    bool predResult = false;
                    try
                    {
                        predResult = predicate(match);
                    }
                    catch (Exception predEx)
                    {
                        s_logger?.Error($"[SDK.{nameof(PACKET_AWAITER)}] Predicate threw for {typeof(TPkt).Name}: {predEx.Message}", predEx);
                        // Predicate exception is considered a handler error — set exception on TCS so caller sees it.
                        try { _ = tcs.TrySetException(predEx); } catch { }
                        return;
                    }

                    if (predResult)
                    {
                        // Found a match; try to set the result (may race with cancellation/disconnect).
                        if (tcs.TrySetResult(match))
                        {
                            s_logger?.Trace($"[SDK.{nameof(PACKET_AWAITER)}] Matched packet {typeof(TPkt).Name} and set result.");
                        }
                    }
                }
            }
            finally
            {
                // Ensure lease is always disposed (SubscribeTemp contract).
                try { buffer.Dispose(); } catch { /* swallow */ }
            }
        }

        void OnDisconnected(object? _, Exception ex)
        {
            Exception exToSet = ex ?? new InvalidOperationException($"Disconnected while waiting for {typeof(TPkt).Name}.");
            try { _ = tcs.TrySetException(exToSet); } catch { /* swallow */ }

            s_logger?.Warn($"[SDK.{nameof(PACKET_AWAITER)}] Disconnected while awaiting {typeof(TPkt).Name}: {exToSet.Message}");
        }
    }
}
