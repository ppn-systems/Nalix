// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Diagnostics;
using Nalix.Common.Networking.Packets.Abstractions;
using Nalix.Common.Networking.Transport;
using Nalix.Common.Shared.Caching;
using Nalix.Framework.Injection;
using Nalix.SDK.Transport.Extensions;
using Nalix.Shared.Extensions;

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
    internal static async System.Threading.Tasks.Task<TPkt> AwaitAsync<TPkt>(
        IClientConnection client,
        System.Func<TPkt, System.Boolean> predicate,
        System.Int32 timeoutMs,
        System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task> sendAsync,
        System.Threading.CancellationToken ct)
        where TPkt : class, IPacket
    {
        // Parameter validation
        System.ArgumentNullException.ThrowIfNull(client);
        System.ArgumentNullException.ThrowIfNull(predicate);
        System.ArgumentNullException.ThrowIfNull(sendAsync);

        if (timeoutMs < 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(timeoutMs), "timeoutMs must be >= 0 (0 = infinite)");
        }

        System.Threading.Tasks.TaskCompletionSource<TPkt> tcs = new(
            System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);

        using var lcts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(ct);

        if (timeoutMs > 0)
        {
            lcts.CancelAfter(timeoutMs);
        }

        // Register cancellation -> cancel the TCS. Use 'using' for the registration (not 'await using').
        await using var reg = lcts.Token.Register(() =>
        {
            try { tcs.TrySetCanceled(lcts.Token); } catch { /* swallow */ }
        });

        s_logger?.Trace($"[SDK.{nameof(PACKET_AWAITER)}] Subscribing for {typeof(TPkt).Name} (timeout={timeoutMs}ms).");

        // Subscribe BEFORE sending — no missed responses regardless of server latency.
        using var sub = client.SubscribeTemp(OnMessageReceived, OnDisconnected);

        // Delegate to caller for the actual send (e.g. client.SendAsync, SendControlAsync, …)
        try
        {
            s_logger?.Debug($"[SDK.{nameof(PACKET_AWAITER)}] Invoking send delegate for expected {typeof(TPkt).Name}.");
            await sendAsync(lcts.Token).ConfigureAwait(false);
            s_logger?.Trace($"[SDK.{nameof(PACKET_AWAITER)}] send delegate completed for {typeof(TPkt).Name}.");
        }
        catch (System.Exception sendEx)
        {
            // Ensure awaiting tasks are signalled about the send failure.
            try { tcs.TrySetException(sendEx); } catch { /* swallow */ }
            s_logger?.Error($"[SDK.{nameof(PACKET_AWAITER)}] send delegate threw: {sendEx.Message}", sendEx);
            throw;
        }

        try
        {
            return await tcs.Task.ConfigureAwait(false);
        }
        catch (System.Threading.Tasks.TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            s_logger?.Debug($"[SDK.{nameof(PACKET_AWAITER)}] Timeout waiting for {typeof(TPkt).Name} after {timeoutMs}ms.");
            throw new System.TimeoutException($"No {typeof(TPkt).Name} received within {timeoutMs} ms.");
        }
        catch (System.Threading.Tasks.TaskCanceledException) when (ct.IsCancellationRequested)
        {
            s_logger?.Debug($"[SDK.{nameof(PACKET_AWAITER)}] Operation cancelled by caller while waiting for {typeof(TPkt).Name}.");
            throw new System.OperationCanceledException(ct);
        }

        // Local handlers

        void OnMessageReceived(System.Object? _, IBufferLease buffer)
        {
            // Ownership: caller of SubscribeTemp provides an IBufferLease; dispose after use.
            try
            {
                // Try deserialize — protect from codec exceptions.
                System.Boolean ok;
                IPacket? p = null;
                try
                {
                    ok = TcpSession.Catalog.TryDeserialize(buffer.Span, out p!);
                }
                catch (System.Exception dex)
                {
                    s_logger?.Error($"[SDK.{nameof(PACKET_AWAITER)}] Deserialization error while awaiting {typeof(TPkt).Name}: {dex.Message}", dex);
                    // If deserialization fails repeatedly, we do not cancel the whole await — just ignore this buffer.
                    return;
                }

                if (!ok || p is null)
                {
                    // Not a recognizable packet — ignore.
                    return;
                }

                if (p is TPkt match)
                {
                    System.Boolean predResult = false;
                    try
                    {
                        predResult = predicate(match);
                    }
                    catch (System.Exception predEx)
                    {
                        s_logger?.Error($"[SDK.{nameof(PACKET_AWAITER)}] Predicate threw for {typeof(TPkt).Name}: {predEx.Message}", predEx);
                        // Predicate exception is considered a handler error — set exception on TCS so caller sees it.
                        try { tcs.TrySetException(predEx); } catch { }
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

        void OnDisconnected(System.Object? _, System.Exception ex)
        {
            var exToSet = ex ?? new System.InvalidOperationException($"Disconnected while waiting for {typeof(TPkt).Name}.");
            try { tcs.TrySetException(exToSet); } catch { /* swallow */ }

            s_logger?.Warn($"[SDK.{nameof(PACKET_AWAITER)}] Disconnected while awaiting {typeof(TPkt).Name}: {exToSet.Message}");
        }
    }
}