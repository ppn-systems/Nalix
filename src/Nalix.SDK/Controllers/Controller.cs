// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Packets;
using Nalix.Common.Protocols;
using Nalix.Framework.Injection;
using Nalix.Framework.Time;
using Nalix.SDK.Remote;
using Nalix.Shared.Memory.Pooling;
using Nalix.Shared.Messaging.Controls;

namespace Nalix.SDK.Controllers;

/// <summary>
/// Client-side controller that handles server-to-client Control frames (CONTROL).
/// It replies PONG to server PING, tracks client-initiated PINGs to compute RTT when PONG arrives,
/// and surfaces other control messages via events.
/// </summary>
public sealed class Controller()
{
    private readonly ObjectPoolManager _pool = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>();

    // seq -> clientMonoTicksAtSend
    private readonly System.Collections.Concurrent.ConcurrentDictionary<System.UInt32, System.Int64> _pingTracker = new();

    /// <summary>
    /// Raised when a server PING arrives (after auto-responding with PONG).
    /// Provides the inbound sequence id for correlation.
    /// </summary>
    public event System.Action<System.UInt32 /*seq*/> OnPing;

    /// <summary>
    /// Raised when a PONG is received.
    /// If the PONG correlates to a client-initiated PING, rttMs will be &gt;=0; otherwise -1.
    /// </summary>
    public event System.Action<System.UInt32 /*seq*/, System.Int32 /*rttMs*/> OnPong;

    /// <summary>
    /// Raised for ACK correlated to a previous client operation.
    /// </summary>
    public event System.Action<System.UInt32 /*seq*/, ProtocolCode /*reason*/> OnAck;

    /// <summary>
    /// Raised for ERROR/NACK/FAIL/TIMEOUT/DISCONNECT to allow the app to decide next steps.
    /// </summary>
    public event System.Action<ControlType /*type*/, ProtocolCode /*reason*/, System.UInt32 /*seq*/> OnError;

    /// <summary>
    /// Raised for informational controls like NOTICE/HEARTBEAT/SHUTDOWN/RESUME.
    /// </summary>
    public event System.Action<ControlType /*type*/, ProtocolCode /*reason*/, System.UInt32 /*seq*/> OnInfo;

    /// <summary>
    /// Handle an inbound pooled <see cref="Control"/> frame from the network.
    /// The caller owns the pooling lifecycle; this method does not return the object to pool.
    /// </summary>
    public async System.Threading.Tasks.ValueTask<System.Boolean> HandleAsync(
        Control c, System.Threading.CancellationToken ct = default)
    {
        if (c is null)
        {
            return false;
        }

        switch (c.Type)
        {
            case ControlType.PING:
                // Respond PONG (echo sequence)
                await SendPongAsync(c.SequenceId, c.OpCode, c.Transport, ct).ConfigureAwait(false);
                OnPing?.Invoke(c.SequenceId);
                return true;

            case ControlType.PONG:
                {
                    var rtt = TryComputeRttMs(c.SequenceId);
                    OnPong?.Invoke(c.SequenceId, rtt);
                    return true;
                }

            case ControlType.ACK:
                OnAck?.Invoke(c.SequenceId, c.Reason);
                return true;

            case ControlType.ERROR:
            case ControlType.NACK:
            case ControlType.FAIL:
            case ControlType.TIMEOUT:
            case ControlType.DISCONNECT:
                OnError?.Invoke(c.Type, c.Reason, c.SequenceId);
                return true;

            case ControlType.NOTICE:
            case ControlType.HEARTBEAT:
            case ControlType.SHUTDOWN:
            case ControlType.RESUME:
                OnInfo?.Invoke(c.Type, c.Reason, c.SequenceId);
                return true;

            case ControlType.HANDSHAKE:
            case ControlType.NONE:
            case ControlType.RESERVED1:
            case ControlType.RESERVED2:
            default:
                // Unknown/unused types: surface as info for completeness.
                OnInfo?.Invoke(c.Type, c.Reason, c.SequenceId);
                return false;
        }
    }

    /// <summary>
    /// Initiates a client-side PING to the server and tracks RTT by sequence id.
    /// Returns the sequence id used.
    /// </summary>
    public async System.Threading.Tasks.ValueTask<System.UInt32> SendPingAsync(
        System.UInt16 opCode = PacketConstants.OpCodeDefault,
        ProtocolType transport = ProtocolType.TCP,
        System.Threading.CancellationToken ct = default)
    {
        var seq = NextSequenceId();
        var nowMono = Clock.MonoTicksNow();

        var pkt = _pool.Get<Control>();
        try
        {
            pkt.Initialize(opCode, ControlType.PING, seq, ProtocolCode.NONE, transport);
            _pingTracker[seq] = nowMono;
            await InstanceManager.Instance.GetOrCreateInstance<ReliableClient>().SendAsync(pkt, ct);

            return seq;
        }
        finally
        {
            _pool.Return(pkt);
        }
    }

    /// <summary>
    /// Sends a PONG in response to a server PING. Normally called internally by HandleAsync.
    /// </summary>
    public async System.Threading.Tasks.ValueTask SendPongAsync(
        System.UInt32 sequenceId,
        System.UInt16 opCode = PacketConstants.OpCodeDefault,
        ProtocolType transport = ProtocolType.TCP,
        System.Threading.CancellationToken ct = default)
    {
        var pkt = _pool.Get<Control>();
        try
        {
            pkt.Initialize(opCode, ControlType.PONG, sequenceId, ProtocolCode.NONE, transport);
            await InstanceManager.Instance.GetOrCreateInstance<ReliableClient>().SendAsync(pkt, ct);
        }
        finally
        {
            _pool.Return(pkt);
        }
    }

    /// <summary>
    /// Sends an ACK with the provided sequence id and reason.
    /// </summary>
    public async System.Threading.Tasks.ValueTask SendAckAsync(
        System.UInt32 sequenceId,
        ProtocolCode reason = ProtocolCode.NONE,
        System.UInt16 opCode = PacketConstants.OpCodeDefault,
        ProtocolType transport = ProtocolType.TCP,
        System.Threading.CancellationToken ct = default)
    {
        var pkt = _pool.Get<Control>();
        try
        {
            pkt.Initialize(opCode, ControlType.ACK, sequenceId, reason, transport);
            await InstanceManager.Instance.GetOrCreateInstance<ReliableClient>().SendAsync(pkt, ct);
        }
        finally
        {
            _pool.Return(pkt);
        }
    }

    /// <summary>
    /// Try compute RTT in milliseconds for a PONG matching a previous client-initiated PING.
    /// Returns -1 when the PONG cannot be correlated (e.g., unsolicited).
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.Int32 TryComputeRttMs(System.UInt32 seq)
    {
        if (_pingTracker.TryRemove(seq, out var sentMono))
        {
            var nowMono = Clock.MonoTicksNow();
            var deltaTicks = nowMono - sentMono;
            if (deltaTicks <= 0)
            {
                return 0;
            }

            // MonoTicks is environment-specific; assuming ticks are 100ns (like .NET Stopwatch ticks ≠ DateTime ticks).
            // If Clock.MonoTicksNow() returns Stopwatch.GetTimestamp(), you should convert with Stopwatch.Frequency.
            // For simplicity, we assume Clock exposes milliseconds scale via helper. If not, adapt conversion here.
            // Here we convert 100ns ticks -> ms as a fallback.
            System.Int64 ms = deltaTicks / System.TimeSpan.TicksPerMillisecond;
            if (ms <= System.Int32.MaxValue)
            {
                return (System.Int32)ms;
            }

            return System.Int32.MaxValue;
        }
        return -1;
    }

    /// <summary>
    /// Generates a new sequence id. Prefer a monotonic increment per-connection.
    /// Replace with your connection's sequence provider if available.
    /// </summary>
    private static System.UInt32 NextSequenceId()
    {
        // You may have a central Sequence provider in your connection layer.
        // For demo: use time-lowered xor to avoid 0; ensure not equal to 0 if your protocol reserves it.
        var now = (System.UInt32)Clock.UnixMillisecondsNow();
        var seq = now ^ (System.UInt32)System.Environment.TickCount;
        return seq == 0 ? 1u : seq;
    }
}
