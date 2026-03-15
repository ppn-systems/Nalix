// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Networking.Caching;
using Nalix.Common.Networking.Packets.Abstractions;
using Nalix.Common.Networking.Transport;
using Nalix.Framework.Injection;
using Nalix.SDK.Transport.Extensions;
using Nalix.Shared.Extensions;

namespace Nalix.SDK.Transport.Internal;

/// <summary>
/// Internal helper that encapsulates the recurring boilerplate shared by all
/// "subscribe → await matching packet → timeout → unsubscribe" operations:
/// <list type="bullet">
///   <item><see cref="ControlExtensions.AwaitPacketAsync{TPkt}"/></item>
///   <item><see cref="RequestExtensions.RequestAsync{TRequest,TResponse}"/></item>
///   <item><see cref="TimeSyncExtensions.TimeSyncAsync"/></item>
///   <item><see cref="HandshakeExtensions.HandshakeAsync"/></item>
/// </list>
/// </summary>
/// <remarks>
/// Callers subscribe <b>before</b> sending to avoid the race where the server
/// responds before the local handler is registered.
/// Unsubscription is automatic — SubscribeTemp disposes when the
/// <c>using</c> block exits, regardless of outcome.
/// </remarks>
internal static class PacketAwaiter
{
    /// <summary>
    /// Subscribes for a matching packet, invokes <paramref name="sendAsync"/>,
    /// and waits until the first packet of type <typeparamref name="TPkt"/> that
    /// satisfies <paramref name="predicate"/> arrives — or the operation times out / is canceled.
    /// </summary>
    /// <typeparam name="TPkt">The expected response packet type.</typeparam>
    /// <param name="client">The connected client.</param>
    /// <param name="predicate">Correlation filter — return <c>true</c> for the desired packet.</param>
    /// <param name="timeoutMs">Maximum wait time in milliseconds.</param>
    /// <param name="sendAsync">
    /// Async delegate that performs the send. Called <b>after</b> the subscription is registered
    /// so no response can be missed. Receives the linked <see cref="System.Threading.CancellationToken"/>.
    /// </param>
    /// <param name="ct">Caller-supplied cancellation token.</param>
    /// <returns>The first <typeparamref name="TPkt"/> that matches <paramref name="predicate"/>.</returns>
    /// <exception cref="System.TimeoutException">
    /// Thrown when no matching packet arrives within <paramref name="timeoutMs"/>.
    /// </exception>
    /// <exception cref="System.OperationCanceledException">
    /// Thrown when <paramref name="ct"/> is canceled.
    /// </exception>
    /// <exception cref="System.Exception">
    /// Re-throws any exception set via <c>TrySetException</c> (e.g., disconnect during wait).
    /// </exception>
    internal static async System.Threading.Tasks.Task<TPkt> AwaitAsync<TPkt>(
        IClientConnection client,
        System.Func<TPkt, System.Boolean> predicate,
        System.Int32 timeoutMs,
        System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task> sendAsync,
        System.Threading.CancellationToken ct)
        where TPkt : class, IPacket
    {
        IPacketRegistry catalog = InstanceManager.Instance.GetExistingInstance<IPacketRegistry>();

        System.Threading.Tasks.TaskCompletionSource<TPkt> tcs =
            new(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);

        using System.Threading.CancellationTokenSource lcts =
            System.Threading.CancellationTokenSource.CreateLinkedTokenSource(ct);

        if (timeoutMs > 0)
        {
            lcts.CancelAfter(timeoutMs);
        }

        await using System.Threading.CancellationTokenRegistration reg =
            lcts.Token.Register(() => tcs.TrySetCanceled(lcts.Token));

        // Subscribe BEFORE sending — no missed responses regardless of server latency.
        using System.IDisposable sub = client.SubscribeTemp(OnMessageReceived, OnDisconnected);

        // Delegate to caller for the actual send (e.g. client.SendAsync, SendControlAsync, …)
        await sendAsync(lcts.Token).ConfigureAwait(false);

        try
        {
            return await tcs.Task.ConfigureAwait(false);
        }
        catch (System.Threading.Tasks.TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new System.TimeoutException(
                $"No {typeof(TPkt).Name} received within {timeoutMs} ms.");
        }
        catch (System.Threading.Tasks.TaskCanceledException) when (ct.IsCancellationRequested)
        {
            throw new System.OperationCanceledException(ct);
        }

        // ── Local handlers ────────────────────────────────────────────────────

        void OnMessageReceived(System.Object _, IBufferLease buffer)
        {
            using (buffer)
            {
                if (catalog.TryDeserialize(buffer.Span, out IPacket p) &&
                    p is TPkt match &&
                    predicate(match))
                {
                    tcs.TrySetResult(match);
                }
            }
        }

        void OnDisconnected(System.Object _, System.Exception ex)
            => tcs.TrySetException(
                ex ?? new System.InvalidOperationException(
                    $"Disconnected while waiting for {typeof(TPkt).Name}."));
    }
}