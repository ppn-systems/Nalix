// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Attributes;
using Nalix.Common.Caching;
using Nalix.Common.Packets;
using Nalix.Common.Packets.Abstractions;
using Nalix.Common.Protocols;
using Nalix.Common.Serialization;
using Nalix.Framework.Injection;
using Nalix.Shared.Extensions;
using Nalix.Shared.Memory.Pooling;
using Nalix.Shared.Serialization;

namespace Nalix.Shared.Messaging.Text;

/// <summary>
/// Represents a simple text-based packet used for transmitting UTF-8 string content over the network.
/// </summary>
[MagicNumber(FrameMagicCode.TEXT512)]
[SerializePackable(SerializeLayout.Explicit)]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[System.Diagnostics.DebuggerDisplay("TEXT512 OpCode={OpCode}, Length={Length}, Flags={Flags}")]
public class Text512 : FrameBase, IPoolable, IPacketDeserializer<Text512>, IPacketCompressor<Text512>
{
    /// <inheritdoc/>
    public const System.Int32 DynamicSize = 512;

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
    [SerializeOrder(PacketHeaderOffset.DataRegion)]
    public System.String Content { get; set; }

    /// <summary>
    /// Initializes a new <see cref="Text512"/> with empty content.
    /// </summary>
    public Text512()
    {
        Flags = PacketFlags.NONE;
        Content = System.String.Empty;
        Priority = PacketPriority.None;
        Protocol = ProtocolType.NONE;
        OpCode = PacketConstants.OpCodeDefault;
        MagicNumber = (System.UInt32)FrameMagicCode.TEXT512;
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
    /// Deserializes a <see cref="Text512"/> from the specified buffer.
    /// </summary>
    /// <remarks>
    /// <b>Internal infrastructure API.</b> Do not call directly—use the dispatcher/serializer.
    /// </remarks>
    /// <param name="buffer">The source buffer.</param>
    /// <returns>A pooled <see cref="Text512"/> instance.</returns>
    public static Text512 Deserialize(System.ReadOnlySpan<System.Byte> buffer)
    {
        Text512 packet = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                                 .Get<Text512>();

        System.Int32 bytesRead = LiteSerializer.Deserialize(buffer, ref packet);

        return bytesRead == 0
            ? throw new System.InvalidOperationException(
                "Failed to deserialize packet: No bytes were read.")
            : packet;
    }

    /// <summary>
    /// Compresses the packet content (UTF-8 → LZ4 → Base64).
    /// </summary>
    /// <remarks><b>Internal infrastructure API. Do not call directly.</b></remarks>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static Text512 Compress(Text512 packet)
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
    public static Text512 Decompress(Text512 packet)
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
        this.Content = System.String.Empty;
        this.Priority = PacketPriority.None;
        this.Protocol = ProtocolType.NONE;
    }

    /// <inheritdoc/>
    public override System.String ToString()
        => $"TEXT512(OpCode={OpCode}, Length={Length}, Flags={Flags}, " +
           $"Priority={Priority}, Protocol={Protocol}, Content={System.Text.Encoding.UTF8.GetByteCount(Content)} bytes)";
}
