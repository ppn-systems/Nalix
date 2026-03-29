// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.Framework.Random;
using Nalix.Framework.Time;
using Nalix.SDK.Transport.Internal;

namespace Nalix.SDK.Transport.Extensions;

/// <summary>
/// Provides client-side helpers for CONTROL frames, including:
/// a fluent <see cref="ControlBuilder"/>, a one-shot <see cref="PingAsync"/> that returns RTT,
/// and <see cref="AwaitControlAsync"/> to wait for a matching CONTROL without a receive pump.
/// </summary>
/// <remarks>
/// These helpers use <see cref="Clock"/> for stamping and monotonic timing to achieve robust RTT measurements.
/// </remarks>
/// <seealso cref="Control"/>
/// <seealso cref="Clock"/>
/// <seealso cref="TcpSession"/>
[SkipLocalsInit]
public static class ControlExtensions
{
    /// <summary>
    /// A fluent builder for <see cref="Control"/> frames.
    /// Use <see cref="NewControl"/> to create an instance,
    /// then chain configuration methods before calling <see cref="Build"/>.
    /// </summary>
    /// <param name="c"></param>
    /// <remarks>
    /// This is a <see langword="ref struct"/> — it cannot be captured in lambdas or stored on the heap.
    /// Use <see cref="Build"/> to materialize the <see cref="Control"/> before passing it to async code.
    /// </remarks>
    public readonly ref struct ControlBuilder(Control c)
    {
        /// <summary>Sets the sequence identifier.</summary>
        /// <param name="seq">The sequence identifier to assign.</param>
        /// <returns>The current builder.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ControlBuilder WithSeq(uint seq) { c.SequenceId = seq; return this; }

        /// <summary>Sets the reason code.</summary>
        /// <param name="reason">The protocol reason code.</param>
        /// <returns>The current builder.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ControlBuilder WithReason(ProtocolReason reason) { c.Reason = reason; return this; }

        /// <summary>Sets the transport type.</summary>
        /// <param name="tr">The transport type (e.g., <see cref="ProtocolType.TCP"/> or UDP).</param>
        /// <returns>The current builder.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ControlBuilder WithTransport(ProtocolType tr) { c.Protocol = tr; return this; }

        /// <summary>
        /// Stamps the control with the current Unix timestamp (milliseconds) and the sender's monotonic ticks.
        /// Note: <see cref="Control.Initialize(ushort, ControlType, uint, ProtocolReason, ProtocolType)"/> already stamps on construction; call this only to refresh.
        /// </summary>
        /// <returns>The current builder.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ControlBuilder StampNow()
        {
            c.MonoTicks = Clock.MonoTicksNow();
            c.Timestamp = Clock.UnixMillisecondsNow();
            return this;
        }

        /// <summary>Builds and returns the configured <see cref="Control"/> instance.</summary>
        /// <returns>The configured <see cref="Control"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Control Build() => c;
    }

    /// <summary>
    /// Creates a new CONTROL frame with the specified operation code and type.
    /// The frame is pre-stamped with the current time via <see cref="Control.Initialize(ushort, ControlType, uint, ProtocolReason, ProtocolType)"/>.
    /// </summary>
    /// <param name="_">The client connection (unused; provided for fluent extension syntax).</param>
    /// <param name="opCode">The operation code.</param>
    /// <param name="type">The control type.</param>
    /// <param name="transport">The transport type. Default is <see cref="ProtocolType.TCP"/>.</param>
    /// <returns>A <see cref="ControlBuilder"/> initialized with the requested type.</returns>
    /// <example>
    /// <code>
    /// Control ping = client.NewControl(opCode, ControlType.PING).WithSeq(123).Build();
    /// </code>
    /// </example>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ControlBuilder NewControl(
        this IClientConnection _,
        ushort opCode,
        ControlType type,
        ProtocolType transport = ProtocolType.TCP)
    {
        Control c = new();
        // Initialize already stamps MonoTicks + Timestamp internally.
        c.Initialize(opCode, type, sequenceId: 0, reasonCode: ProtocolReason.NONE, transport: transport);
        return new ControlBuilder(c);
    }

    /// <summary>
    /// Awaits until a packet of type <typeparamref name="TPkt"/> satisfying the predicate arrives,
    /// a timeout occurs, or the connection is dropped.
    /// </summary>
    /// <typeparam name="TPkt">The expected packet type.</typeparam>
    /// <param name="client">The connected client.</param>
    /// <param name="predicate">A predicate that returns <c>true</c> for the desired packet.</param>
    /// <param name="timeoutMs">Maximum wait time in milliseconds.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The first matching packet.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="client"/> or <paramref name="predicate"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">Thrown when the client is not connected.</exception>
    /// <exception cref="TimeoutException">Thrown when no matching packet is received within <paramref name="timeoutMs"/>.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="ct"/> is canceled.</exception>
    public static Task<TPkt> AwaitPacketAsync<TPkt>(
        this TcpSessionBase client,
        Func<TPkt, bool> predicate,
        int timeoutMs,
        CancellationToken ct = default)
        where TPkt : class, IPacket
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(predicate);

        if (!client.IsConnected)
        {
            throw new InvalidOperationException("Client not connected.");
        }

        // Delegate all TCS + subscribe + timeout logic to PacketAwaiter.
        // sendAsync = no-op because the caller has already sent (or will send externally).
        return PACKET_AWAITER.AwaitAsync(
            client,
            predicate,
            timeoutMs,
            sendAsync: _ => Task.CompletedTask,
            ct);
    }

    /// <summary>
    /// Sends a PING and awaits the matching PONG.
    /// </summary>
    /// <param name="client">The connected reliable client.</param>
    /// <param name="opCode">The operation code used for the PING/PONG exchange.</param>
    /// <param name="sequenceId">Optional sequence id; if <c>null</c>, a cryptographically random value is generated.</param>
    /// <param name="timeoutMs">Overall timeout in milliseconds.</param>
    /// <param name="syncClock">
    /// If <c>true</c>, synchronizes <see cref="Clock"/> using the server's PONG timestamp with an RTT/2 bias.
    /// </param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>
    /// A tuple of:
    /// <list type="bullet">
    /// <item><description><c>rttMs</c> — round-trip time in milliseconds.</description></item>
    /// <item><description><c>pong</c> — the received PONG control frame.</description></item>
    /// </list>
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="client"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the client is not connected.</exception>
    /// <exception cref="TimeoutException">Thrown when a matching PONG is not received within <paramref name="timeoutMs"/>.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="ct"/> is canceled.</exception>
    /// <remarks>
    /// RTT is computed using monotonic ticks. If the server echoes the sender's <see cref="Control.MonoTicks"/>,
    /// that value is preferred over the locally captured send tick.
    /// </remarks>
    /// <example>
    /// <code>
    /// var (rtt, pong) = await client.PingAsync(opCode: 3, timeoutMs: 2000, syncClock: true, ct: ct);
    /// </code>
    /// </example>
    public static async Task<(double rttMs, Control pong)> PingAsync(
        this TcpSessionBase client,
        ushort opCode,
        uint? sequenceId = null,
        int timeoutMs = 3000,
        bool syncClock = false,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(client);

        if (!client.IsConnected)
        {
            throw new InvalidOperationException("Client not connected.");
        }

        uint seq = sequenceId ?? Csprng.NextUInt32();

        Control ping = client.NewControl(opCode, ControlType.PING)
                             .WithSeq(seq)
                             .Build();

        long sendMono = ping.MonoTicks != 0 ? ping.MonoTicks : Clock.MonoTicksNow();

        // RequestAsync: subscribe → send → await PONG in one race-condition-free call.
        Control pong = await client.RequestAsync<Control, Control>(
            ping,
            predicate: p => p.Type == ControlType.PONG && p.SequenceId == seq,
            timeoutMs: timeoutMs,
            ct: ct).ConfigureAwait(false);

        long nowMono = Clock.MonoTicksNow();
        double rtt = pong.MonoTicks > 0 && pong.MonoTicks <= nowMono
            ? Clock.MonoTicksToMilliseconds(nowMono - pong.MonoTicks)
            : Clock.MonoTicksToMilliseconds(nowMono - sendMono);

        if (syncClock && pong.Timestamp > 0)
        {
            DateTime serverUtc = DateTime.UnixEpoch.AddMilliseconds(pong.Timestamp + (rtt * 0.5));
            _ = Clock.SynchronizeTime(serverUtc);
        }

        return (rtt, pong);
    }

    /// <summary>
    /// Awaits until a CONTROL frame matching the specified predicate is received, or a timeout occurs.
    /// Non-matching packets are ignored.
    /// </summary>
    /// <param name="client">The connected reliable client.</param>
    /// <param name="predicate">A predicate that returns <c>true</c> for the desired CONTROL.</param>
    /// <param name="timeoutMs">The maximum time to wait, in milliseconds.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The first CONTROL packet matching <paramref name="predicate"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="client"/> or <paramref name="predicate"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="TimeoutException">Thrown when no matching CONTROL is received within <paramref name="timeoutMs"/>.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="ct"/> is canceled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<Control> AwaitControlAsync(
        this TcpSessionBase client,
        Func<Control, bool> predicate,
        int timeoutMs,
        CancellationToken ct = default)
        => AwaitPacketAsync(client, predicate, timeoutMs, ct);

    /// <summary>
    /// Sends a CONTROL frame using a fluent configuration callback.
    /// </summary>
    /// <param name="client">The connected reliable client.</param>
    /// <param name="opCode">The operation code.</param>
    /// <param name="type">The control type to send.</param>
    /// <param name="configure">
    /// An optional callback to customize the built <see cref="Control"/> before sending.
    /// Receives the materialized <see cref="Control"/> instance (not the <see langword="ref struct"/> builder)
    /// to avoid stack-capture restrictions.
    /// </param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="client"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the client is not connected.</exception>
    /// <example>
    /// <code>
    /// await client.SendControlAsync(
    ///     opCode: 0,
    ///     type: ControlType.NOTICE,
    ///     configure: ctrl => { ctrl.SequenceId = 42; ctrl.Reason = ProtocolReason.NONE; },
    ///     ct: ct);
    /// </code>
    /// </example>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task SendControlAsync(this TcpSessionBase client, ushort opCode, ControlType type, Action<Control>? configure = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(client);

        if (!client.IsConnected)
        {
            throw new InvalidOperationException("Client not connected.");
        }

        // Materialize the Control from the builder first; ref structs cannot be lambda-captured.
        Control ctrl = client.NewControl(opCode, type).Build();
        configure?.Invoke(ctrl);
        return client.SendAsync(ctrl, ct);
    }
}
