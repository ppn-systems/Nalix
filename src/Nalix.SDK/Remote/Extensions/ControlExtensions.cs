// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Packets.Abstractions;
using Nalix.Common.Protocols;           // ControlType, ProtocolType
using Nalix.Framework.Time;             // Clock
using Nalix.Shared.Messaging.Controls;  // Control

namespace Nalix.SDK.Remote.Extensions;

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
/// <seealso cref="ReliableClient"/>
public static class ControlExtensions
{
    /// <summary>
    /// A fluent builder for <see cref="Control"/> frames.
    /// Use <see cref="NewControl(ReliableClient, System.UInt16, ControlType, ProtocolType)"/> to create an instance,
    /// then chain configuration methods before calling <see cref="Build"/>.
    /// </summary>
    public readonly ref struct ControlBuilder(Control c)
    {
        /// <summary>
        /// Sets the sequence identifier.
        /// </summary>
        /// <param name="seq">The sequence identifier to assign.</param>
        /// <returns>The current builder.</returns>
        public ControlBuilder WithSeq(System.UInt32 seq) { c.SequenceId = seq; return this; }

        /// <summary>
        /// Sets the reason code.
        /// </summary>
        /// <param name="reason">The protocol reason code.</param>
        /// <returns>The current builder.</returns>
        public ControlBuilder WithReason(ProtocolCode reason) { c.Reason = reason; return this; }

        /// <summary>
        /// Sets the transport type.
        /// </summary>
        /// <param name="tr">The transport type (e.g., <see cref="ProtocolType.TCP"/> or <see cref="ProtocolType.UDP"/>).</param>
        /// <returns>The current builder.</returns>
        public ControlBuilder WithTransport(ProtocolType tr) { c.Transport = tr; return this; }

        /// <summary>
        /// Stamps the control with the current Unix timestamp (milliseconds) and the sender's monotonic ticks.
        /// </summary>
        /// <returns>The current builder.</returns>
        public ControlBuilder StampNow() { c.MonoTicks = Clock.MonoTicksNow(); c.Timestamp = Clock.UnixMillisecondsNow(); return this; }

        /// <summary>
        /// Builds and returns the configured <see cref="Control"/> instance.
        /// </summary>
        /// <returns>The configured <see cref="Control"/>.</returns>
        public Control Build() => c;
    }

    /// <summary>
    /// Creates a new CONTROL frame with the specified type and default metadata.
    /// </summary>
    /// <param name="_">The client (unused; provided for fluent extension syntax).</param>
    /// <param name="opCode">The operation code.</param>
    /// <param name="type">The control type.</param>
    /// <param name="transport">The transport type. Default is <see cref="ProtocolType.TCP"/>.</param>
    /// <returns>A <see cref="ControlBuilder"/> initialized with the requested type.</returns>
    /// <example>
    /// <code>
    /// ControlBuilder c = client.NewControl(ControlType.PING).WithSeq(123).StampNow().Build();
    /// </code>
    /// </example>
    public static ControlBuilder NewControl(this ReliableClient _, System.UInt16 opCode, ControlType type, ProtocolType transport = ProtocolType.TCP)
    {
        Control c = new();
        c.Initialize(opCode, type, sequenceId: 0, reasonCode: ProtocolCode.NONE, transport: transport);
        return new ControlBuilder(c);
    }

    /// <summary>
    /// Sends a PING and awaits the matching PONG (same <see cref="Control.SequenceId"/>).
    /// </summary>
    /// <param name="client">The connected reliable client.</param>
    /// <param name="opCode">The operation code.</param>
    /// <param name="sequenceId">Optional sequence id; if <c>null</c>, a value is generated.</param>
    /// <param name="timeoutMs">Overall timeout in milliseconds for send and wait operations.</param>
    /// <param name="syncClock">
    /// If <c>true</c>, synchronizes <see cref="Clock"/> using the server's PONG timestamp with an RTT/2 bias.
    /// </param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>
    /// A tuple containing:
    /// <list type="bullet">
    /// <item><description><c>rttMs</c> — the computed round-trip time in milliseconds.</description></item>
    /// <item><description><c>pong</c> — the received PONG control.</description></item>
    /// </list>
    /// </returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="client"/> is <c>null</c>.</exception>
    /// <exception cref="System.InvalidOperationException">Thrown when the client is not connected.</exception>
    /// <exception cref="System.TimeoutException">Thrown when a matching PONG is not received within <paramref name="timeoutMs"/>.</exception>
    /// <exception cref="System.OperationCanceledException">Thrown when <paramref name="ct"/> is canceled.</exception>
    /// <remarks>
    /// RTT is computed using monotonic ticks. If the server echoes the sender's ticks in <see cref="Control.MonoTicks"/>,
    /// that value is preferred; otherwise a locally captured send tick is used.
    /// </remarks>
    /// <example>
    /// <code>
    /// var (rtt, pong) = await client.PingAsync(timeoutMs: 2000, syncClock: true, ct);
    /// </code>
    /// </example>
    public static async System.Threading.Tasks.Task<(System.Double rttMs, Control pong)> PingAsync(
        this ReliableClient client,
        System.UInt16 opCode,
        System.UInt32? sequenceId = null,
        System.Int32 timeoutMs = 3000,
        System.Boolean syncClock = false,
        System.Threading.CancellationToken ct = default)
    {
        System.ArgumentNullException.ThrowIfNull(client);
        if (!client.IsConnected)
        {
            throw new System.InvalidOperationException("Client not connected.");
        }

        // Sequence: generate if not provided
        System.UInt32 seq = sequenceId ?? unchecked((System.UInt32)System.Environment.TickCount);

        // Build + send PING (ControlType.PING)  (enum: PING/PONG)  :contentReference[oaicite:2]{index=2}
        var ping = client.NewControl(opCode, ControlType.PING)
                         .WithSeq(seq)
                         .StampNow()
                         .Build();

        // We capture send mono now (Initialize already stamped, but grab again here for robustness)
        System.Int64 sendMono = ping.MonoTicks != 0 ? ping.MonoTicks : Clock.MonoTicksNow();
        using var lcts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(ct);
        lcts.CancelAfter(timeoutMs);

        await client.SendAsync(ping, lcts.Token).ConfigureAwait(false);

        // Await matching PONG (same SequenceId)
        Control pong = await client.AwaitControlAsync(
            predicate: c => c.Type == ControlType.PONG && c.SequenceId == seq,  // PONG match  :contentReference[oaicite:3]{index=3}
            timeoutMs: timeoutMs,
            ct: lcts.Token).ConfigureAwait(false);

        // Compute RTT (prefer echoed MonoTicks if server echoes send mono; else fallback to local)
        System.Int64 nowMono = Clock.MonoTicksNow();
        System.Double rtt = pong.MonoTicks > 0 && pong.MonoTicks <= nowMono
            ? Clock.MonoTicksToMilliseconds(nowMono - pong.MonoTicks)
            : Clock.MonoTicksToMilliseconds(nowMono - sendMono);

        // Optional time sync using server's Unix ms + RTT/2
        if (syncClock && pong.Timestamp > 0)
        {
            var serverUtc = System.DateTime.UnixEpoch.AddMilliseconds(pong.Timestamp + (rtt * 0.5));
            _ = Clock.SynchronizeTime(serverUtc);
        }

        return (rtt, pong);
    }

    /// <summary>
    /// Receives packets until a CONTROL matching the specified predicate is observed, or a timeout occurs.
    /// Non-matching packets are ignored by this method.
    /// </summary>
    /// <param name="client">The connected reliable client.</param>
    /// <param name="predicate">A predicate that returns <c>true</c> for the desired CONTROL.</param>
    /// <param name="timeoutMs">The maximum time to wait, in milliseconds.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The first CONTROL packet that matches <paramref name="predicate"/>.</returns>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="client"/> or <paramref name="predicate"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="System.TimeoutException">Thrown when no matching CONTROL is received within <paramref name="timeoutMs"/>.</exception>
    /// <exception cref="System.OperationCanceledException">Thrown when <paramref name="ct"/> is canceled.</exception>
    /// <remarks>
    /// This helper does not enqueue packets into an <c>Incoming</c> buffer; it directly awaits from the stream.
    /// </remarks>
    public static async System.Threading.Tasks.Task<Control> AwaitControlAsync(
        this ReliableClient client,
        System.Func<Control, System.Boolean> predicate,
        System.Int32 timeoutMs,
        System.Threading.CancellationToken ct = default)
    {
        System.ArgumentNullException.ThrowIfNull(client);
        System.ArgumentNullException.ThrowIfNull(predicate);

        using var lcts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(ct);
        lcts.CancelAfter(timeoutMs);

        try
        {
            while (true)
            {
                IPacket p = await client.ReceiveAsync(lcts.Token).ConfigureAwait(false);
                if (p is Control c && predicate(c))
                {
                    return c;
                }
                // else: ignore and continue
            }
        }
        catch (System.OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Canceled by timeout
            throw new System.TimeoutException("No matching CONTROL was received within the allotted timeout.");
        }
    }

    /// <summary>
    /// Sends a CONTROL frame with a single call using a fluent configuration callback.
    /// </summary>
    /// <param name="client">The connected reliable client.</param>
    /// <param name="opCode">The operation code.</param>
    /// <param name="type">The control type to send.</param>
    /// <param name="configure">An optional configuration callback to customize the control being sent.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous send operation.</returns>
    /// <exception cref=" System.ArgumentNullException">Thrown when <paramref name="client"/> is <c>null</c>.</exception>
    /// <example>
    /// <code>
    /// await client.SendControlAsync(
    ///     ControlType.NOTICE,
    ///     b => b.WithSeq(42).WithReason(ProtocolCode.NONE).StampNow(),
    ///     ct);
    /// </code>
    /// </example>
    public static System.Threading.Tasks.Task SendControlAsync(
        this ReliableClient client,
        System.UInt16 opCode,
        ControlType type,
        System.Action<ControlBuilder> configure = null,
        System.Threading.CancellationToken ct = default)
    {
        System.ArgumentNullException.ThrowIfNull(client);
        var b = client.NewControl(opCode, type);
        configure?.Invoke(b);
        return client.SendAsync(b.Build(), ct);
    }

    /// <summary>
    /// Builds and sends a DISCONNECT control (TCP by default).
    /// </summary>
    /// <param name="client">The connected reliable client.</param>
    /// <param name="opCode">The operation code.</param>
    /// <param name="seq">The optional sequence identifier.</param>
    /// <param name="tr">The transport type. Default is <see cref="ProtocolType.TCP"/>.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous send operation.</returns>
    /// <example>
    /// <code>
    /// await client.SendDisconnectAsync(seq: 7, tr: ProtocolType.TCP, ct);
    /// </code>
    /// </example>
    public static System.Threading.Tasks.Task SendDisconnectAsync(
        this ReliableClient client,
        System.UInt16 opCode,
        System.UInt32 seq = 0,
        ProtocolType tr = ProtocolType.TCP,
        System.Threading.CancellationToken ct = default)
        => client.SendControlAsync(opCode, ControlType.DISCONNECT, b => b.WithSeq(seq).WithTransport(tr).StampNow(), ct);
}
