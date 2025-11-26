// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Nalix.Common.Abstractions;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Serialization;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Objects;
using Nalix.Framework.Serialization;

namespace Nalix.Framework.DataFrames.TextFrames;

/// <summary>
/// Represents a simple text-based packet used for transmitting UTF-8 string content over the network.
/// </summary>
[SerializePackable(SerializeLayout.Explicit)]
[ExcludeFromCodeCoverage]
[DebuggerDisplay("TEXT1024 OpCode={OpCode}, Length={Length}, Flags={Flags}")]
public class Text1024 : FrameBase, IPoolable, IPacketDeserializer<Text1024>
{
    /// <inheritdoc/>
    public const int DynamicSize = 1024;

    /// <summary>
    /// Gets the total serialized length in bytes, including header and content.
    /// </summary>
    [SerializeIgnore]
    public override ushort Length =>
        (ushort)(PacketConstants.HeaderSize + Encoding.UTF8.GetByteCount(this.Content ?? string.Empty));

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
        this.Flags = PacketFlags.NONE;
        this.Protocol = ProtocolType.NONE;
        this.Content = string.Empty;
        this.Priority = PacketPriority.NONE;
        this.OpCode = PacketConstants.OpcodeDefault;
    }

    /// <summary>Initializes the packet with content and transport protocol.</summary>
    /// <param name="content">The UTF-8 string to store.</param>
    /// <param name="transport">The target transport protocol.</param>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public void Initialize(
        string content,
        ProtocolType transport = ProtocolType.TCP)
    {
        if (content.Length > DynamicSize)
        {
            throw new ArgumentOutOfRangeException(nameof(content), $"Text supports at most {DynamicSize} bytes.");
        }

        this.Protocol = transport;
        this.Content = content ?? string.Empty;
    }

    /// <summary>
    /// Deserializes a <see cref="Text1024"/> from the specified buffer.
    /// </summary>
    /// <remarks>
    /// <b>Internal infrastructure API.</b> Do not call directly—use the dispatcher/serializer.
    /// </remarks>
    /// <param name="buffer">The source buffer.</param>
    /// <returns>A pooled <see cref="Text1024"/> instance.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static Text1024 Deserialize(ReadOnlySpan<byte> buffer)
    {
        Text1024 packet = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                                  .Get<Text1024>();

        int bytesRead = LiteSerializer.Deserialize(buffer, ref packet);
        if (bytesRead == 0)
        {
            InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .Return(packet);
            throw new InvalidOperationException("Failed to deserialize packet: No bytes were read.");
        }

        return packet;
    }

    /// <inheritdoc/>
    public override byte[] Serialize() => LiteSerializer.Serialize(this);

    /// <inheritdoc/>
    public override int Serialize(Span<byte> buffer) => LiteSerializer.Serialize(this, buffer);

    /// <summary>Resets this instance to its default state for pooling reuse.</summary>
    public override void ResetForPool()
    {
        this.Flags = PacketFlags.NONE;
        this.Protocol = ProtocolType.NONE;
        this.Content = string.Empty;
        this.Priority = PacketPriority.NONE;
    }

    /// <inheritdoc/>
    public override string ToString()
        => $"TEXT1024(OpCode={this.OpCode}, Length={this.Length}, Flags={this.Flags}, " +
           $"Priority={this.Priority}, Protocol={this.Protocol}, Content={Encoding.UTF8.GetByteCount(this.Content)} bytes)";
}
