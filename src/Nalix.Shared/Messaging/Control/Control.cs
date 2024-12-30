// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Attributes;
using Nalix.Common.Connection.Protocols;
using Nalix.Common.Enums;
using Nalix.Common.Packets;
using Nalix.Common.Packets.Abstractions;
using Nalix.Common.Packets.Enums;
using Nalix.Common.Security.Enums;
using Nalix.Common.Serialization;
using Nalix.Common.Serialization.Attributes;
using Nalix.Framework.Time;
using Nalix.Shared.Injection;
using Nalix.Shared.Memory.Pooling;
using Nalix.Shared.Messaging.Binary;
using Nalix.Shared.Serialization;

namespace Nalix.Shared.Messaging.Control;

/// <summary>
/// Represents a binary data packet used for transmitting raw bytes over the network.
/// </summary>
[PipelineManagedTransform]
[MagicNumber(MagicNumbers.Control)]
[SerializePackable(SerializeLayout.Explicit)]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[System.Diagnostics.DebuggerDisplay("Control OpCode={OpCode}, Length={Length}, Flags={Flags}")]
public sealed class Control : FrameBase, IPacketReasoned, IPacketSequenced, IPacketTransformer<Control>
{
    /// <summary>
    /// Gets the total length of the serialized packet in bytes, including header and content.
    /// </summary>
    [SerializeIgnore]
    public override System.UInt16 Length =>
        PacketConstants.HeaderSize
        + sizeof(System.UInt32)  // SequenceId
        + sizeof(System.UInt16)  // ReasonCode
        + sizeof(ControlType)    // ControlType
        + sizeof(System.Int64)   // Timestamp
        + sizeof(System.Int64);  // MonoTicks

    /// <summary>
    /// Gets or sets the sequence identifier for this packet.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.End + 0)]
    public System.UInt32 SequenceId { get; set; }

    /// <summary>
    /// Gets or sets the reason code associated with this control packet.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.End + 1)]
    public System.UInt16 ReasonCode { get; set; }

    /// <summary>
    /// Gets or sets the binary content of the packet.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.End + 2)]
    public ControlType Type { get; set; }

    /// <summary>
    /// Gets or sets the timestamp associated with this packet.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.End + 3)]
    public System.Int64 Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the monotonic timestamp (in ticks) for RTT measurement.
    /// </summary>
    [SerializeOrder(PacketHeaderOffset.End + 4)]
    public System.Int64 MonoTicks { get; set; }

    /// <summary>
    /// Initializes a new <see cref="Binary128"/> with empty content.
    /// </summary>
    public Control()
    {
        this.Timestamp = 0;
        this.MonoTicks = 0;
        this.SequenceId = 0;
        this.ReasonCode = 0;
        this.Type = ControlType.Null; // Default type, can be changed later
        this.Flags = PacketFlags.None;
        this.Priority = PacketPriority.Urgent;
        this.Transport = TransportProtocol.Null;
        this.OpCode = PacketConstants.OpCodeDefault;
        this.MagicNumber = (System.UInt32)MagicNumbers.Control;
    }

    /// <summary>
    /// Initializes the control packet with full metadata.
    /// </summary>
    /// <param name="type">The control message type.</param>
    /// <param name="sequenceId">The sequence identifier (optional, default = 0).</param>
    /// <param name="reasonCode">The reason code (optional, default = 0).</param>
    /// <param name="transport">The transport protocol (default = TCP).</param>
    public void Initialize(
        ControlType type,
        System.UInt32 sequenceId = 0,
        System.UInt16 reasonCode = 0,
        TransportProtocol transport = TransportProtocol.Tcp)
    {
        this.Type = type;
        this.Transport = transport;
        this.SequenceId = sequenceId;
        this.ReasonCode = reasonCode;
        this.MonoTicks = Clock.MonoTicksNow();
        this.Timestamp = Clock.UnixMillisecondsNow();
    }

    /// <summary>
    /// Initializes the packet with binary data and a transport protocol.
    /// </summary>
    /// <param name="type">Binary content of the packet.</param>
    /// <param name="transport">The target transport protocol.</param>
    public void Initialize(ControlType type, TransportProtocol transport = TransportProtocol.Tcp) => Initialize(type, 0, 0, transport);

    /// <summary>
    /// Deserializes a <see cref="Binary128"/> from the specified buffer.
    /// </summary>
    /// <param name="buffer">The source buffer.</param>
    /// <returns>A pooled <see cref="Binary128"/> instance.</returns>
    public static Control Deserialize(in System.ReadOnlySpan<System.Byte> buffer)
    {
        Control packet = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                                 .Get<Control>();

        System.Int32 bytesRead = LiteSerializer.Deserialize(buffer, ref packet);

        return bytesRead == 0
            ? throw new System.InvalidOperationException(
                "Failed to deserialize packet: No bytes were read.")
            : packet;
    }

    /// <inheritdoc/>
    /// <remarks><b>Internal infrastructure API. Do not call directly.</b></remarks>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    [System.Obsolete("Internal infrastructure API. Encryption is handled by the pipeline.", error: true)]
    public static Control Encrypt(Control packet, System.Byte[] key, SymmetricAlgorithmType algorithm)
        => throw new System.NotImplementedException();

    /// <inheritdoc/>
    /// <remarks><b>Internal infrastructure API. Do not call directly.</b></remarks>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    [System.Obsolete("Internal infrastructure API. Decryption is handled by the pipeline.", error: true)]
    public static Control Decrypt(Control packet, System.Byte[] key, SymmetricAlgorithmType algorithm)
        => throw new System.NotImplementedException();

    /// <inheritdoc/>
    /// <remarks><b>Internal infrastructure API. Do not call directly.</b></remarks>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    [System.Obsolete("Internal infrastructure API. Decryption is handled by the pipeline.", error: true)]
    public static Control Compress(Control packet) => throw new System.NotImplementedException();

    /// <inheritdoc/>
    /// <remarks><b>Internal infrastructure API. Do not call directly.</b></remarks>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    [System.Obsolete("Internal infrastructure API. Decryption is handled by the pipeline.", error: true)]
    public static Control Decompress(Control packet) => throw new System.NotImplementedException();

    /// <summary>
    /// Resets this instance to its default state for pooling reuse.
    /// </summary>
    public override void ResetForPool()
    {
        this.Timestamp = 0;
        this.MonoTicks = 0;
        this.SequenceId = 0;
        this.ReasonCode = 0;
        this.Type = ControlType.Null;
        this.Flags = PacketFlags.None;
        this.Priority = PacketPriority.Urgent;
        this.Transport = TransportProtocol.Null;
    }

    /// <inheritdoc/>
    public override System.String ToString() =>
        $"Control(Op={OpCode}, Len={Length}, Flg={Flags}, Pri={Priority}, " +
        $"Tr={Transport}, Seq={SequenceId}, Rsn={ReasonCode}, Typ={Type}, Ts={Timestamp}, Mono={MonoTicks})";
}
