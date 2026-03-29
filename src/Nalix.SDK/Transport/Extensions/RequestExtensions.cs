// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Diagnostics;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.Injection;
using Nalix.SDK.Configuration;
using Nalix.SDK.Transport.Internal;

namespace Nalix.SDK.Transport.Extensions;

/// <summary>
/// Provides request-response helpers for <see cref="IClientConnection"/>.
/// Combines a one-shot subscription with a send operation so callers can
/// <c>await</c> a typed reply without wiring boilerplate by hand.
/// </summary>
/// <remarks>
/// <para>
/// Internally delegates the subscribe → await → timeout → unsubscribe cycle to
/// <see cref="PACKET_AWAITER"/>, which handles deserialization errors, predicate
/// exceptions, and disconnect guards consistently across all SDK extension methods.
/// </para>
/// <para>
/// <b>Threading model:</b> callbacks are invoked on the FRAME_READER background thread.
/// Marshal to the main thread before touching Unity GameObjects or WPF/MAUI UI controls.
/// </para>
/// <para>
/// <b>Retry:</b> only <see cref="TimeoutException"/> triggers a retry.
/// Fatal errors (send failure, disconnect) propagate immediately.
/// </para>
/// <para>
/// <see cref="RequestAsync{TRequest,TResponse}"/> is the safe, race-condition-free way to
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
    /// Sends <paramref name="request"/> and awaits the first response of type
    /// <typeparamref name="TResponse"/> that satisfies <paramref name="predicate"/>.
    /// </summary>
    /// <typeparam name="TRequest">The outgoing packet type.</typeparam>
    /// <typeparam name="TResponse">The expected response packet type.</typeparam>
    /// <param name="client">The connected client.</param>
    /// <param name="request">The packet to send.</param>
    /// <param name="predicate">
    /// Correlation predicate — return <c>true</c> for the packet that matches this request.
    /// Typically checks a sequence/correlation ID.
    /// </param>
    /// <param name="timeoutMs">
    /// Total timeout in milliseconds covering both the send and the await.
    /// Default is <c>5000</c> ms.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The first matching <typeparamref name="TResponse"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="client"/>, <paramref name="request"/>,
    /// or <paramref name="predicate"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the client is not connected.
    /// </exception>
    /// <exception cref="TimeoutException">
    /// Thrown when no matching response is received within <paramref name="timeoutMs"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when <paramref name="ct"/> is canceled.
    /// </exception>
    /// <example>
    /// <code>
    /// var response = await client.RequestAsync&lt;LoginRequest, LoginResponse&gt;(
    ///     new LoginRequest { CorrelationId = seq, Username = "phuc" },
    ///     predicate: r => r.CorrelationId == seq,
    ///     timeoutMs: 3000,
    ///     ct: ct);
    /// </code>
    /// </example>
    public static Task<TResponse> RequestAsync<TRequest, TResponse>(
        this IClientConnection client,
        TRequest request,
        Func<TResponse, bool> predicate,
        int timeoutMs = 5000,
        CancellationToken ct = default)
        where TRequest : class, IPacket
        where TResponse : class, IPacket
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(predicate);

        if (!client.IsConnected)
        {
            throw new InvalidOperationException("Client is not connected.");
        }

        // PacketAwaiter handles: subscribe → send → await → timeout → unsubscribe.
        return PACKET_AWAITER.AwaitAsync(
            client,
            predicate,
            timeoutMs,
            sendAsync: token => client.SendAsync(request, token),
            ct);
    }

    /// <summary>
    /// Convenience overload when request and response share the same type
    /// (e.g., echo-style protocols).
    /// </summary>
    /// <typeparam name="TPacket">Both request and response type.</typeparam>
    /// <param name="client">The connected client.</param>
    /// <param name="request">The packet to send.</param>
    /// <param name="predicate">Correlation predicate.</param>
    /// <param name="timeoutMs">Timeout in milliseconds. Default is <c>5000</c> ms.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The first matching response packet.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<TPacket> RequestAsync<TPacket>(
        this IClientConnection client,
        TPacket request,
        Func<TPacket, bool> predicate,
        int timeoutMs = 5000,
        CancellationToken ct = default)
        where TPacket : class, IPacket => RequestAsync<TPacket, TPacket>(client, request, predicate, timeoutMs, ct);

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
    /// <paramref name="client"/> is not a <see cref="TcpSessionBase"/>.
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
        this IClientConnection client,
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
            throw new InvalidOperationException(
                $"[SDK.RequestAsync<{typeof(TResponse).Name}>] Client is not connected.");
        }

        // Fail-fast: Encrypt requires BaseTcpSession — check before any attempt.
        if (options.Encrypt && client is not TcpSessionBase)
        {
            throw new ArgumentException(
                $"[SDK.RequestAsync<{typeof(TResponse).Name}>] RequestOptions.Encrypt=true requires TcpSessionBase. Got: {client.GetType().Name}", nameof(client));
        }

        Func<TResponse, bool> effectivePredicate = predicate ?? (_ => true);
        Exception? lastException = null;
        int totalAttempts = options.RetryCount + 1;

        for (int attempt = 1; attempt <= totalAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                // Delegate the full subscribe → send → await → timeout → unsubscribe cycle
                // to PACKET_AWAITER, which handles deserialization errors, predicate exceptions,
                // and disconnect guards consistently across all SDK helpers.
                TResponse result = await PACKET_AWAITER.AwaitAsync(
                    client,
                    predicate: effectivePredicate,
                    timeoutMs: options.TimeoutMs,
                    sendAsync: token => options.Encrypt
                        ? ((TcpSessionBase)client).SendAsync(request, encrypt: true, token)
                        : client.SendAsync(request, token),
                    ct).ConfigureAwait(false);

                if (attempt > 1)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Info($"[SDK.RequestAsync<{typeof(TResponse).Name}>] Succeeded on attempt {attempt}/{totalAttempts}.");
                }

                return result;
            }
            catch (TimeoutException tex) when (attempt < totalAttempts)
            {
                // Only TimeoutException is retryable.
                // OperationCanceledException, InvalidOperationException, etc. propagate immediately.
                lastException = tex;
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Warn($"[SDK.RequestAsync<{typeof(TResponse).Name}>] Attempt {attempt}/{totalAttempts} timed out, retrying...");
            }
        }

        throw new TimeoutException(
            $"[SDK.RequestAsync<{typeof(TResponse).Name}>] No response after {totalAttempts} attempt(s) (timeout={options.TimeoutMs}ms each).", lastException);
    }
}
