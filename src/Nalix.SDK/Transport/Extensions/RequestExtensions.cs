// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Exceptions;
using Nalix.Common.Networking.Packets;
using Nalix.SDK.Options;

namespace Nalix.SDK.Transport.Extensions;

/// <summary>
/// Provides request-response helpers for <see cref="TransportSession"/>.
/// Combines a one-shot subscription with a send operation so callers can
/// <c>await</c> a typed reply without wiring boilerplate by hand.
/// </summary>
/// <remarks>
/// <para>
/// Internally delegates the subscribe -> await -> timeout -> unsubscribe cycle to
/// <see cref="PacketAwaiter"/>, which handles deserialization errors, predicate
/// exceptions, and disconnect guards consistently across all SDK extension methods.
/// </para>
/// <para>
/// <b>Threading model:</b> callbacks are invoked on the FrameReader background thread.
/// Marshal to the main thread before touching Unity GameObjects or WPF/MAUI UI controls.
/// </para>
/// <para>
/// <b>Retry:</b> only <see cref="TimeoutException"/> triggers a retry.
/// Fatal errors (send failure, disconnect) propagate immediately.
/// </para>
/// <para>
/// <see cref="RequestAsync{TResponse}"/> is the safe, race-condition-free way to
/// send a packet and await a correlated reply. It subscribes <b>before</b> sending — eliminating
/// the window where the server response could arrive before the local handler is registered.
/// </para>
/// <para>
/// Compared to calling <c>SendAsync</c> then <c>AwaitPacketAsync</c> sequentially, this method
/// guarantees no missed responses even under high concurrency or very low-latency servers.
/// </para>
/// </remarks>
[SkipLocalsInit]
public static class RequestExtensions
{
    /// <summary>
    /// Sends <paramref name="request"/> and waits for the first incoming packet
    /// of type <typeparamref name="TResponse"/> that satisfies <paramref name="predicate"/>.
    /// Retry and encryption behaviour is controlled by <paramref name="options"/>.
    /// </summary>
    /// <typeparam name="TResponse">Expected response packet type.</typeparam>
    /// <param name="client">Connected client session.</param>
    /// <param name="request">Packet to send. Must not be <see langword="null"/>.</param>
    /// <param name="options">
    /// Timeout, retry, and encryption settings.
    /// <see langword="null"/> falls back to <see cref="RequestOptions.Default"/>.
    /// </param>
    /// <param name="predicate">
    /// Optional response filter. <see langword="null"/> accepts the first
    /// <typeparamref name="TResponse"/> that arrives.
    /// </param>
    /// <param name="ct">Cancellation token for the entire operation (all attempts).</param>
    /// <returns>The first matching response packet.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="client"/> or <paramref name="request"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Client not connected, or <c>SendAsync</c> returned <see langword="false"/>.
    /// </exception>
    /// <exception cref="TimeoutException">
    /// No response arrived within the allotted timeout on all attempts.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="ct"/> was cancelled, or the connection dropped mid-wait.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <see cref="RequestOptions.Encrypt"/> is <see langword="true"/> but
    /// <paramref name="client"/> is not a <see cref="TcpSession"/>.
    /// </exception>
    /// <example>
    /// <code>
    /// // 1. Simplest — default options
    /// var reply = await client.RequestAsync&lt;LoginResponse&gt;(loginRequest);
    ///
    /// // 2. Custom options, fluent
    /// var opts = RequestOptions.Default
    ///     .WithTimeout(3_000)
    ///     .WithRetry(2)
    ///     .WithEncrypt();
    /// var reply = await client.RequestAsync&lt;LoginResponse&gt;(loginRequest, opts);
    ///
    /// // 3. With correlation predicate
    /// var reply = await client.RequestAsync&lt;TradeResponse&gt;(
    ///     tradeRequest,
    ///     RequestOptions.Default.WithTimeout(2_000),
    ///     predicate: r => r.RequestId == tradeRequest.Id);
    /// </code>
    /// </example>
    public static async Task<TResponse> RequestAsync<TResponse>(
        this TransportSession client,
        IPacket request,
        RequestOptions? options = null,
        Func<TResponse, bool>? predicate = null,
        CancellationToken ct = default)
        where TResponse : class, IPacket
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(request);

        options ??= RequestOptions.Default;
        options.Validate();

        if (!client.IsConnected)
        {
            throw new NetworkException(
                $"[SDK.RequestAsync<{typeof(TResponse).Name}>] Client is not connected.");
        }

        // Fail-fast: Encrypt requires BaseTcpSession — check before any attempt.
        if (options.Encrypt && client is not TcpSession)
        {
            throw new ArgumentException(
                $"[SDK.RequestAsync<{typeof(TResponse).Name}>] RequestOptions.Encrypt=true requires TcpSession. Got: {client.GetType().Name}", nameof(client));
        }

        Exception? lastException = null;
        int totalAttempts = options.RetryCount + 1;
        Func<TResponse, bool> effectivePredicate = predicate ?? (_ => true);

        for (int attempt = 1; attempt <= totalAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                // Delegate the full subscribe -> send -> await -> timeout -> unsubscribe cycle
                // to PACKET_AWAITER, which handles deserialization errors, predicate exceptions,
                // and disconnect guards consistently across all SDK helpers.
                TResponse result = await PacketAwaiter.AwaitAsync(
                    client,
                    predicate: effectivePredicate,
                    timeoutMs: options.TimeoutMs,
                    sendAsync: token => options.Encrypt
                        ? ((TcpSession)client).SendAsync(request, encrypt: true, token)
                        : client.SendAsync(request, token),
                    ct).ConfigureAwait(false);

                return result;
            }
            catch (TimeoutException tex) when (attempt < totalAttempts)
            {
                // Only TimeoutException is retryable.
                // OperationCanceledException, InvalidOperationException, etc. propagate immediately.
                lastException = tex;
            }
        }

        throw new TimeoutException(
            $"[SDK.RequestAsync<{typeof(TResponse).Name}>] No response after {totalAttempts} attempt(s) (timeout={options.TimeoutMs}ms each).", lastException);
    }
}
