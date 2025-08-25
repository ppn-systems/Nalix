// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Attributes;
using Nalix.Common.Infrastructure.Caching;
using Nalix.Common.Messaging.Packets;
using Nalix.Common.Messaging.Packets.Abstractions;
using Nalix.Common.Messaging.Protocols;
using Nalix.Common.Serialization;
using Nalix.Framework.Injection;
using Nalix.Shared.Extensions;
using Nalix.Shared.Memory.Pooling;
using Nalix.Shared.Serialization;

namespace Nalix.Shared.Messaging.Text;

/// <summary>
/// Represents a simple text-based packet used for transmitting UTF-8 string content over the network.
/// </summary>
[MagicNumber(ProtocolMagic.TEXT1024)]
[SerializePackable(SerializeLayout.Explicit)]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[System.Diagnostics.DebuggerDisplay("TEXT1024 OP_CODE={OP_CODE}, Length={Length}, FLAGS={FLAGS}")]
public class Text1024 : FrameBase, IPoolable, IPacketDeserializer<Text1024>, IPacketCompressor<Text1024>
{
    /// <inheritdoc/>
    public const System.Int32 DynamicSize = 1024;

    /// <summary>
    /// Gets the total serialized length in bytes, including header and content.
    /// </summary>
    [SerializeIgnore]
    public override System.UInt16 Length =>
        (System.UInt16)(PacketConstants.HeaderSize + System.Text.Encoding.UTF8.GetByteCount(Content ?? System.String.Empty));

    /// <summary>
    /// Gets or sets the UTF-8 string content of the packet.
    /// </summary>
    [SerializeDynamicSize(DynamicSize)]
    [SerializeOrder(PacketHeaderOffset.DATA_REGION)]
    public System.String Content { get; set; }

    /// <summary>
    /// Initializes a new <see cref="Text1024"/> with empty content.
    /// </summary>
    public Text1024()
    {
        Flags = PacketFlags.NONE;
        Protocol = ProtocolType.NONE;
        Content = System.String.Empty;
        Priority = PacketPriority.NONE;
        OpCode = PacketConstants.OPCODE_DEFAULT;
        MagicNumber = (System.UInt32)ProtocolMagic.TEXT1024;
    }

    /// <summary>Initializes the packet with content and transport protocol.</summary>
    /// <param name="content">The UTF-8 string to store.</param>
    /// <param name="transport">The target transport protocol.</param>
    public void Initialize(
        System.String content,
        ProtocolType transport = ProtocolType.TCP)
    {
        if (content.Length > DynamicSize)
        {
            throw new System.ArgumentOutOfRangeException(nameof(content), $"Text supports at most {DynamicSize} bytes.");
        }

        this.Protocol = transport;
        this.Content = content ?? System.String.Empty;
    }

    /// <summary>
    /// Deserializes a <see cref="Text1024"/> from the specified buffer.
    /// </summary>
    /// <remarks>
    /// <b>Internal infrastructure API.</b> Do not call directly—use the dispatcher/serializer.
    /// </remarks>
    /// <param name="buffer">The source buffer.</param>
    /// <returns>A pooled <see cref="Text1024"/> instance.</returns>
    public static Text1024 Deserialize(System.ReadOnlySpan<System.Byte> buffer)
    {
        Text1024 packet = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                                  .Get<Text1024>();

        System.Int32 bytesRead = LiteSerializer.Deserialize(buffer, ref packet);
        if (bytesRead == 0)
        {
            InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .Return(packet);
            throw new System.InvalidOperationException("Failed to deserialize packet: No bytes were read.");
        }

        return packet;
    }

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
        packet.Flags = packet.Flags.AddFlag(PacketFlags.COMPRESSED);

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
        packet.Flags = packet.Flags.RemoveFlag(PacketFlags.COMPRESSED);

        return packet;
    }

    /// <inheritdoc/>
    public override System.Byte[] Serialize() => LiteSerializer.Serialize(this);

    /// <inheritdoc/>
    public override System.Int32 Serialize(System.Span<System.Byte> buffer) => LiteSerializer.Serialize(this, buffer);

    /// <summary>Resets this instance to its default state for pooling reuse.</summary>
    public override void ResetForPool()
    {
        this.Flags = PacketFlags.NONE;
        this.Protocol = ProtocolType.NONE;
        this.Content = System.String.Empty;
        this.Priority = PacketPriority.NONE;
    }

    /// <inheritdoc/>
    public override System.String ToString()
        => $"TEXT1024(OP_CODE={OpCode}, Length={Length}, FLAGS={Flags}, " +
           $"PRIORITY={Priority}, Protocol={Protocol}, Content={System.Text.Encoding.UTF8.GetByteCount(Content)} bytes)";
}
