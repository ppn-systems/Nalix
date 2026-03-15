// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Networking.Packets.Abstractions;
using Nalix.Common.Networking.Transport;
using Nalix.SDK.Transport.Internal;

namespace Nalix.SDK.Transport.Extensions;

/// <summary>
/// Provides a type-safe request/response pattern for <see cref="IClientConnection"/>.
/// </summary>
/// <remarks>
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
[System.Runtime.CompilerServices.SkipLocalsInit]
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
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="client"/>, <paramref name="request"/>,
    /// or <paramref name="predicate"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when the client is not connected.
    /// </exception>
    /// <exception cref="System.TimeoutException">
    /// Thrown when no matching response is received within <paramref name="timeoutMs"/>.
    /// </exception>
    /// <exception cref="System.OperationCanceledException">
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
    public static System.Threading.Tasks.Task<TResponse> RequestAsync<TRequest, TResponse>(
        this IClientConnection client,
        TRequest request,
        System.Func<TResponse, System.Boolean> predicate,
        System.Int32 timeoutMs = 5000,
        System.Threading.CancellationToken ct = default)
        where TRequest : class, IPacket
        where TResponse : class, IPacket
    {
        System.ArgumentNullException.ThrowIfNull(client);
        System.ArgumentNullException.ThrowIfNull(request);
        System.ArgumentNullException.ThrowIfNull(predicate);

        if (!client.IsConnected)
        {
            throw new System.InvalidOperationException("Client is not connected.");
        }

        // PacketAwaiter handles: subscribe → send → await → timeout → unsubscribe.
        return PacketAwaiter.AwaitAsync(
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Threading.Tasks.Task<TPacket> RequestAsync<TPacket>(
        this IClientConnection client,
        TPacket request,
        System.Func<TPacket, System.Boolean> predicate,
        System.Int32 timeoutMs = 5000,
        System.Threading.CancellationToken ct = default)
        where TPacket : class, IPacket
        => RequestAsync<TPacket, TPacket>(client, request, predicate, timeoutMs, ct);
}