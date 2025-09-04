// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Attributes;
using Nalix.Common.Caching;
using Nalix.Common.Packets;
using Nalix.Common.Packets.Abstractions;
using Nalix.Common.Packets.Enums;
using Nalix.Common.Protocols;
using Nalix.Common.Serialization;
using Nalix.Common.Serialization.Attributes;
using Nalix.Framework.Time;
using Nalix.Shared.Injection;
using Nalix.Shared.Memory.Pooling;
using Nalix.Shared.Serialization;

namespace Nalix.Shared.Messaging.Time;

/// <summary>
/// Time synchronization packet (NTP-style).
/// Request: client sets T0; server fills T1/T2 and echoes SequenceId.
/// Response: server returns T1/T2; client measures T3 locally.
/// </summary>
[PipelineManagedTransform]
[MagicNumber(FrameMagicCode.TIME_SYNC)]
[SerializePackable(SerializeLayout.Explicit)]
[System.Diagnostics.DebuggerDisplay("TIME_SYNC {Stage} Seq={SequenceId}")]
public sealed class TimeSync : FrameBase, IPacketSequenced, IPoolable, IPacketDeserializer<TimeSync>
{
    /// <summary>
    /// Stage of the time-sync exchange.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.DataRegion + 0)]
    public TimeSyncStage Stage { get; set; }

    /// <summary>
    /// Correlation id.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.DataRegion + 1)]
    public System.UInt32 SequenceId { get; set; }

    /// <summary>t0 (client send, Unix ms) set by client in REQUEST.</summary>
    [SerializeOrder(PacketHeaderOffset.DataRegion + 2)]
    public System.Int64 T0ClientSend { get; set; }

    /// <summary>t1 (server receive, Unix ms) set by server in RESPONSE.</summary>
    [SerializeOrder(PacketHeaderOffset.DataRegion + 3)]
    public System.Int64 T1ServerRecv { get; set; }

    /// <summary>t2 (server send, Unix ms) set by server in RESPONSE.</summary>
    [SerializeOrder(PacketHeaderOffset.DataRegion + 4)]
    public System.Int64 T2ServerSend { get; set; }

    /// <summary>Monotonic ticks at client send (optional diagnostics).</summary>
    [SerializeOrder(PacketHeaderOffset.DataRegion + 5)]
    public System.Int64 MonoClientSend { get; set; }

    /// <summary>Monotonic ticks at server receive (optional diagnostics).</summary>
    [SerializeOrder(PacketHeaderOffset.DataRegion + 6)]
    public System.Int64 MonoServerRecv { get; set; }

    /// <summary>Monotonic ticks at server send (optional diagnostics).</summary>
    [SerializeOrder(PacketHeaderOffset.DataRegion + 7)]
    public System.Int64 MonoServerSend { get; set; }

    /// <inheritdoc/>
    [SerializeIgnore]
    public override System.UInt16 Length =>
        PacketConstants.HeaderSize
        + sizeof(TimeSyncStage)
        + sizeof(System.UInt32)
        + (sizeof(System.Int64) * 6);

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeSync"/> class.
    /// </summary>
    public TimeSync()
    {
        // Defaults
        this.Stage = TimeSyncStage.REQUEST;
        this.SequenceId = 0;
        this.T0ClientSend = 0;
        this.T1ServerRecv = 0;
        this.T2ServerSend = 0;
        this.MonoClientSend = 0;
        this.MonoServerRecv = 0;
        this.MonoServerSend = 0;

        this.Flags = PacketFlags.None;
        this.Priority = PacketPriority.Urgent;
        this.Transport = ProtocolType.TCP;
        this.OpCode = PacketConstants.OpCodeDefault;
        this.MagicNumber = (System.UInt32)FrameMagicCode.TIME_SYNC;
    }

    /// <summary>Create a REQUEST (client side).</summary>
    public void InitializeRequest(System.UInt32 seq, ProtocolType transport = ProtocolType.TCP)
    {
        this.Stage = TimeSyncStage.REQUEST;
        this.Transport = transport;
        this.SequenceId = seq;

        this.T0ClientSend = Clock.UnixMillisecondsNow();
        this.MonoClientSend = Clock.MonoTicksNow();

        // Clear server fields
        this.T1ServerRecv = 0;
        this.T2ServerSend = 0;
        this.MonoServerRecv = 0;
        this.MonoServerSend = 0;
    }

    /// <summary>Fill a RESPONSE (server side).</summary>
    public void InitializeResponseFrom(TimeSync req, ProtocolType transport = ProtocolType.TCP)
    {
        this.Stage = TimeSyncStage.RESPONSE;
        this.Transport = transport;
        this.SequenceId = req.SequenceId;

        this.T0ClientSend = req.T0ClientSend;
        this.MonoClientSend = req.MonoClientSend;

        this.T1ServerRecv = Clock.UnixMillisecondsNow();
        this.MonoServerRecv = Clock.MonoTicksNow();

        // Sending immediately after computing t1
        this.T2ServerSend = Clock.UnixMillisecondsNow();
        this.MonoServerSend = Clock.MonoTicksNow();
    }

    /// <inheritdoc/>
    public static TimeSync Deserialize(System.ReadOnlySpan<System.Byte> buffer)
    {
        var pkt = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                         .Get<TimeSync>();

        var bytesRead = LiteSerializer.Deserialize(buffer, ref pkt);
        return bytesRead == 0
            ? throw new System.InvalidOperationException("Failed to deserialize TimeSync.")
            : pkt;
    }

    /// <inheritdoc/>
    public override void ResetForPool()
    {
        this.Stage = TimeSyncStage.REQUEST;
        this.SequenceId = 0;
        this.T0ClientSend = 0;
        this.T1ServerRecv = 0;
        this.T2ServerSend = 0;
        this.MonoClientSend = 0;
        this.MonoServerRecv = 0;
        this.MonoServerSend = 0;

        this.Flags = PacketFlags.None;
        this.Priority = PacketPriority.Urgent;
        this.Transport = ProtocolType.TCP;
    }

    /// <inheritdoc/>
    public override System.String ToString() =>
        $"TIME_SYNC({Stage}) Seq={SequenceId} T0={T0ClientSend} T1={T1ServerRecv} T2={T2ServerSend}";
}

/// <summary>
/// Stage of the time sync exchange.
/// </summary>
public enum TimeSyncStage : System.Byte
{
    /// <inheritdoc/>
    REQUEST = 0,

    /// <inheritdoc/>
    RESPONSE = 1
}
