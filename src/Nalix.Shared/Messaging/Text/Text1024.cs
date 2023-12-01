// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Attributes;
using Nalix.Common.Connection.Protocols;
using Nalix.Common.Packets;
using Nalix.Common.Packets.Abstractions;
using Nalix.Common.Packets.Enums;
using Nalix.Common.Security.Enums;
using Nalix.Common.Serialization;
using Nalix.Common.Serialization.Attributes;
using Nalix.Shared.Extensions;
using Nalix.Shared.Injection;
using Nalix.Shared.LZ4.Extensions;
using Nalix.Shared.Memory.Pooling;
using Nalix.Shared.Serialization;

namespace Nalix.Shared.Messaging.Text;

/// <summary>
/// Represents a simple text-based packet used for transmitting UTF-8 string content over the network.
/// </summary>
[MagicNumber(MagicNumbers.Text1024)]
[SerializePackable(SerializeLayout.Explicit)]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[System.Diagnostics.DebuggerDisplay("Text256 OpCode={OpCode}, Length={Length}, Flags={Flags}")]
public class Text1024 : IPacket, IPacketTransformer<Text1024>
{
    /// <inheritdoc/>
    public const System.Int32 DynamicSize = 1024;

    /// <inheritdoc/>
    public static System.Int32 Size => PacketConstants.HeaderSize + DynamicSize;

    /// <summary>Gets the total serialized length in bytes, including header and content.</summary>
    [SerializeIgnore]
    public System.UInt16 Length =>
        (System.UInt16)(PacketConstants.HeaderSize + System.Text.Encoding.UTF8.GetByteCount(Content ?? System.String.Empty));

    /// <summary>Gets the magic number used to identify the packet format.</summary>
    [SerializeOrder(PacketHeaderOffset.MagicNumber)]
    public System.UInt32 MagicNumber { get; set; }

    /// <summary>Gets the operation code (OpCode) of this packet.</summary>
    [SerializeOrder(PacketHeaderOffset.OpCode)]
    public System.UInt16 OpCode { get; set; }

    /// <summary>Gets the flags associated with this packet.</summary>
    [SerializeOrder(PacketHeaderOffset.Flags)]
    public PacketFlags Flags { get; set; }

    /// <summary>Gets the packet priority.</summary>
    [SerializeOrder(PacketHeaderOffset.Priority)]
    public PacketPriority Priority { get; set; }

    /// <summary>Gets the transport protocol (e.g., TCP/UDP) this packet targets.</summary>
    [SerializeOrder(PacketHeaderOffset.Transport)]
    public TransportProtocol Transport { get; set; }

    /// <summary>Gets or sets the UTF-8 string content of the packet.</summary>
    [SerializeDynamicSize(DynamicSize)]
    [SerializeOrder(PacketHeaderOffset.End)]
    public System.String Content { get; set; }

    /// <summary>Initializes a new <see cref="Text256"/> with empty content.</summary>
    public Text1024()
    {
        Flags = PacketFlags.None;
        Content = System.String.Empty;
        Priority = PacketPriority.Normal;
        Transport = TransportProtocol.Null;
        OpCode = PacketConstants.OpCodeDefault;
        MagicNumber = (System.UInt32)MagicNumbers.Text256;
    }

    /// <summary>Initializes the packet with content and transport protocol.</summary>
    /// <param name="content">The UTF-8 string to store.</param>
    /// <param name="transport">The target transport protocol.</param>
    public void Initialize(
        System.String content,
        TransportProtocol transport = TransportProtocol.Tcp)
    {
        if (content.Length > DynamicSize)
        {
            throw new System.ArgumentOutOfRangeException(nameof(content), $"Text supports at most {DynamicSize} bytes.");
        }

        this.Transport = transport;
        this.Content = content ?? System.String.Empty;
    }

    /// <summary>Serializes this packet to a newly allocated byte array.</summary>
    public System.Byte[] Serialize() => LiteSerializer.Serialize(this);

    /// <summary>Serializes this packet into the provided destination buffer.</summary>
    /// <param name="buffer">The destination buffer. Must be large enough.</param>
    public void Serialize(System.Span<System.Byte> buffer) => LiteSerializer.Serialize(this, buffer);

    /// <summary>
    /// Deserializes a <see cref="Text256"/> from the specified buffer.
    /// </summary>
    /// <remarks>
    /// <b>Internal infrastructure API.</b> Do not call directly—use the dispatcher/serializer.
    /// </remarks>
    /// <param name="buffer">The source buffer.</param>
    /// <returns>A pooled <see cref="Text256"/> instance.</returns>
    public static Text1024 Deserialize(in System.ReadOnlySpan<System.Byte> buffer)
    {
        Text1024 packet = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                                  .Get<Text1024>();

        System.Int32 bytesRead = LiteSerializer.Deserialize(buffer, ref packet);

        return bytesRead == 0
            ? throw new System.InvalidOperationException(
                "Failed to deserialize packet: No bytes were read.")
            : packet;
    }

    /// <remarks><b>Internal infrastructure API. Do not call directly.</b></remarks>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    [System.Obsolete("Internal infrastructure API. Encryption is handled by the pipeline.", error: true)]
    public static Text1024 Encrypt(Text1024 packet, System.Byte[] key, SymmetricAlgorithmType algorithm)
        => throw new System.NotImplementedException();

    /// <remarks><b>Internal infrastructure API. Do not call directly.</b></remarks>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    [System.Obsolete("Internal infrastructure API. Decryption is handled by the pipeline.", error: true)]
    public static Text1024 Decrypt(Text1024 packet, System.Byte[] key, SymmetricAlgorithmType algorithm)
        => throw new System.NotImplementedException();

    /// <summary>
    /// Compresses the packet content (UTF-8 → LZ4 → Base64).
    /// </summary>
    /// <remarks><b>Internal infrastructure API. Do not call directly.</b></remarks>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static Text1024 Compress(Text1024 packet)
    {
        if (packet?.Content == null)
        {
            throw new System.ArgumentNullException(nameof(packet));
        }

        packet.Content = packet.Content.CompressToBase64();
        packet.Flags = packet.Flags.AddFlag(PacketFlags.Compressed);

        return packet;
    }

    /// <summary>
    /// Decompresses the packet content (Base64 → LZ4 → UTF-8).
    /// </summary>
    /// <remarks><b>Internal infrastructure API. Do not call directly.</b></remarks>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static Text1024 Decompress(Text1024 packet)
    {
        if (packet?.Content == null)
        {
            throw new System.ArgumentNullException(nameof(packet));
        }

        packet.Content = packet.Content.DecompressFromBase64();
        packet.Flags = packet.Flags.RemoveFlag(PacketFlags.Compressed);

        return packet;
    }

    /// <summary>Resets this instance to its default state for pooling reuse.</summary>
    public void ResetForPool()
    {
        this.Flags = PacketFlags.None;
        this.Content = System.String.Empty;
        this.Priority = PacketPriority.Normal;
        this.Transport = TransportProtocol.Null;
    }

    /// <inheritdoc/>
    public override System.String ToString()
        => $"Text256(OpCode={OpCode}, Length={Length}, Flags={Flags}, " +
           $"Priority={Priority}, Transport={Transport}, Content={System.Text.Encoding.UTF8.GetByteCount(Content)} bytes)";
}
