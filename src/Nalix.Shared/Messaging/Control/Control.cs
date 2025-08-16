// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Attributes;
using Nalix.Common.Connection.Protocols;
using Nalix.Common.Packets;
using Nalix.Common.Packets.Enums;
using Nalix.Common.Packets.Interfaces;
using Nalix.Common.Security.Cryptography.Enums;
using Nalix.Common.Serialization;
using Nalix.Common.Serialization.Attributes;
using Nalix.Framework.Time;
using Nalix.Shared.Memory.Pooling;
using Nalix.Shared.Messaging.Binary;
using Nalix.Shared.Serialization;

namespace Nalix.Shared.Messaging.Control;

/// <summary>
/// Represents a binary data packet used for transmitting raw bytes over the network.
/// </summary>
[MagicNumber(MagicNumbers.Handshake)]
[SerializePackable(SerializeLayout.Explicit)]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[System.Diagnostics.DebuggerDisplay("Binary128 OpCode={OpCode}, Length={Length}, Flags={Flags}")]
public class Control : IPacket, IPacketTransformer<Control>
{
    /// <summary>
    /// Gets the total length of the serialized packet in bytes, including header and content.
    /// </summary>
    [SerializeIgnore]
    public System.UInt16 Length =>
        PacketConstants.HeaderSize + sizeof(ControlType) + sizeof(System.Int64);

    /// <summary>
    /// Gets the magic number used to identify the packet format.
    /// </summary>
    [SerializeOrder(0)]
    public System.UInt32 MagicNumber { get; set; }

    /// <summary>
    /// Gets the operation code (OpCode) of this packet.
    /// </summary>
    [SerializeOrder(4)]
    public System.UInt16 OpCode { get; set; }

    /// <summary>
    /// Gets the flags associated with this packet.
    /// </summary>
    [SerializeOrder(6)]
    public PacketFlags Flags { get; set; }

    /// <summary>
    /// Gets the packet priority.
    /// </summary>
    [SerializeOrder(7)]
    public PacketPriority Priority { get; set; }

    /// <summary>
    /// Gets the transport protocol (e.g., TCP/UDP) this packet targets.
    /// </summary>
    [SerializeOrder(8)]
    public TransportProtocol Transport { get; set; }

    /// <summary>
    /// Gets or sets the binary content of the packet.
    /// </summary>
    [SerializeOrder(9)]
    public ControlType Type { get; set; }

    /// <summary>
    /// Gets or sets the timestamp associated with this packet.
    /// </summary>
    [SerializeOrder(10)]
    public System.Int64 Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the monotonic timestamp (in ticks) for RTT measurement.
    /// </summary>
    [SerializeOrder(11)]
    public System.Int64 MonoTicks { get; set; }

    /// <summary>
    /// Initializes a new <see cref="Binary128"/> with empty content.
    /// </summary>
    public Control()
    {
        Timestamp = 0;
        MonoTicks = 0;
        Type = ControlType.Ping; // Default type, can be changed later
        Flags = PacketFlags.None;
        Priority = PacketPriority.Normal;
        Transport = TransportProtocol.Null;
        OpCode = PacketConstants.OpCodeDefault;
        MagicNumber = (System.UInt32)MagicNumbers.Handshake;
    }

    /// <summary>
    /// Initializes the packet with binary data.
    /// </summary>
    /// <param name="type">Binary content of the packet.</param>
    public void Initialize(ControlType type) => Initialize(type, TransportProtocol.Tcp);

    /// <summary>
    /// Initializes the packet with binary data and a transport protocol.
    /// </summary>
    /// <param name="type">Binary content of the packet.</param>
    /// <param name="transport">The target transport protocol.</param>
    public void Initialize(ControlType type, TransportProtocol transport = TransportProtocol.Tcp)
    {
        this.Type = type;
        this.Transport = transport;
        this.MonoTicks = Clock.MonoTicksNow();
        this.Timestamp = Clock.UnixMillisecondsNow();
    }

    /// <summary>
    /// Serializes the packet to a newly allocated byte array.
    /// </summary>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Byte[] Serialize() => LiteSerializer.Serialize(this);

    /// <summary>
    /// Serializes the packet into the provided destination buffer.
    /// </summary>
    /// <param name="buffer">The destination buffer. Must be large enough.</param>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Serialize(System.Span<System.Byte> buffer) => LiteSerializer.Serialize(this, buffer);

    /// <summary>
    /// Deserializes a <see cref="Binary128"/> from the specified buffer.
    /// </summary>
    /// <param name="buffer">The source buffer.</param>
    /// <returns>A pooled <see cref="Binary128"/> instance.</returns>
    public static Control Deserialize(in System.ReadOnlySpan<System.Byte> buffer)
    {
        Control packet = ObjectPoolManager.Instance.Get<Control>();
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
    public static Control Compress(Control packet)
        => throw new System.NotImplementedException();

    /// <inheritdoc/>
    /// <remarks><b>Internal infrastructure API. Do not call directly.</b></remarks>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    [System.Obsolete("Internal infrastructure API. Decryption is handled by the pipeline.", error: true)]
    public static Control Decompress(Control packet)
        => throw new System.NotImplementedException();

    /// <summary>
    /// Resets this instance to its default state for pooling reuse.
    /// </summary>
    public void ResetForPool()
    {
        this.Timestamp = 0;
        MonoTicks = 0;
        this.Type = ControlType.Ping;
        this.Flags = PacketFlags.None;
        this.Priority = PacketPriority.Normal;
        this.Transport = TransportProtocol.Null;
    }

    /// <inheritdoc/>
    public override System.String ToString() =>
        $"Control(OpCode={OpCode}, Length={Length}, Flags={Flags}, " +
        $"Priority={Priority}, Transport={Transport}, Type={Type}, Timestamp={Timestamp})";
}
