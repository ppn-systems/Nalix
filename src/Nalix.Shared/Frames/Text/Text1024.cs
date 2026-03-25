// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Serialization;
using Nalix.Common.Shared;
using Nalix.Framework.Injection;
using Nalix.Shared.Memory.Objects;
using Nalix.Shared.Serialization;

namespace Nalix.Shared.Frames.Text;

/// <summary>
/// Represents a simple text-based packet used for transmitting UTF-8 string content over the network.
/// </summary>
[SerializePackable(SerializeLayout.Explicit)]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[System.Diagnostics.DebuggerDisplay("TEXT1024 OpCode={OpCode}, Length={Length}, Flags={Flags}")]
public class Text1024 : FrameBase, IPoolable, IPacketDeserializer<Text1024>
{
    /// <inheritdoc/>
    public const int DynamicSize = 1024;

    /// <summary>
    /// Gets the total serialized length in bytes, including header and content.
    /// </summary>
    [SerializeIgnore]
    public override ushort Length =>
        (ushort)(PacketConstants.HeaderSize + System.Text.Encoding.UTF8.GetByteCount(Content ?? string.Empty));

    /// <summary>
    /// Gets or sets the UTF-8 string content of the packet.
    /// </summary>
    [SerializeDynamicSize(DynamicSize)]
    [SerializeOrder(PacketHeaderOffset.Region)]
    public string Content { get; set; }

    /// <summary>
    /// Initializes a new <see cref="Text1024"/> with empty content.
    /// </summary>
    public Text1024()
    {
        Flags = PacketFlags.NONE;
        Protocol = ProtocolType.NONE;
        Content = string.Empty;
        Priority = PacketPriority.NONE;
        OpCode = PacketConstants.OpcodeDefault;
    }

    /// <summary>Initializes the packet with content and transport protocol.</summary>
    /// <param name="content">The UTF-8 string to store.</param>
    /// <param name="transport">The target transport protocol.</param>
    /// <exception cref="System.ArgumentOutOfRangeException"></exception>
    public void Initialize(
        string content,
        ProtocolType transport = ProtocolType.TCP)
    {
        if (content.Length > DynamicSize)
        {
            throw new System.ArgumentOutOfRangeException(nameof(content), $"Text supports at most {DynamicSize} bytes.");
        }

        Protocol = transport;
        Content = content ?? string.Empty;
    }

    /// <summary>
    /// Deserializes a <see cref="Text1024"/> from the specified buffer.
    /// </summary>
    /// <remarks>
    /// <b>Internal infrastructure API.</b> Do not call directly—use the dispatcher/serializer.
    /// </remarks>
    /// <param name="buffer">The source buffer.</param>
    /// <returns>A pooled <see cref="Text1024"/> instance.</returns>
    /// <exception cref="System.InvalidOperationException"></exception>
    public static Text1024 Deserialize(System.ReadOnlySpan<byte> buffer)
    {
        Text1024 packet = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                                  .Get<Text1024>();

        int bytesRead = LiteSerializer.Deserialize(buffer, ref packet);
        if (bytesRead == 0)
        {
            InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .Return(packet);
            throw new System.InvalidOperationException("Failed to deserialize packet: No bytes were read.");
        }

        return packet;
    }

    /// <inheritdoc/>
    public override byte[] Serialize() => LiteSerializer.Serialize(this);

    /// <inheritdoc/>
    public override int Serialize(System.Span<byte> buffer) => LiteSerializer.Serialize(this, buffer);

    /// <summary>Resets this instance to its default state for pooling reuse.</summary>
    public override void ResetForPool()
    {
        Flags = PacketFlags.NONE;
        Protocol = ProtocolType.NONE;
        Content = string.Empty;
        Priority = PacketPriority.NONE;
    }

    /// <inheritdoc/>
    public override string ToString()
        => $"TEXT1024(OpCode={OpCode}, Length={Length}, Flags={Flags}, " +
           $"Priority={Priority}, Protocol={Protocol}, Content={System.Text.Encoding.UTF8.GetByteCount(Content)} bytes)";
}
