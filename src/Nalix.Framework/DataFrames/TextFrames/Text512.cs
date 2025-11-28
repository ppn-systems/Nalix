// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Serialization;

namespace Nalix.Framework.DataFrames.TextFrames;

/// <summary>
/// Represents a UTF-8 text packet that supports up to 512 dynamic bytes.
/// Implemented using PacketBase for consistent serialization and pooling.
/// </summary>
[SerializePackable(SerializeLayout.Explicit)]
[ExcludeFromCodeCoverage]
[DebuggerDisplay("TEXT512 OpCode={OpCode}, Length={Length}, Flags={Flags}")]
public sealed class Text512 : PacketBase<Text512>
{
    /// <summary>
    /// Maximum number of UTF-8 bytes allowed in <see cref="Content"/>.
    /// </summary>
    public const int DynamicSize = 512;

    /// <summary>
    /// Gets or sets the UTF-8 text content of this packet.
    /// </summary>
    [SerializeDynamicSize(DynamicSize)]
    [SerializeOrder(PacketHeaderOffset.Region)]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Initializes a new <see cref="Text512"/> with default header fields.
    /// </summary>
    public Text512() : base()
    {
        this.Protocol = ProtocolType.NONE;
        this.Flags = PacketFlags.NONE;
        this.Priority = PacketPriority.NONE;
        this.OpCode = PacketConstants.OpcodeDefault;
        this.Content = string.Empty;
    }

    /// <summary>
    /// Initializes the packet with text content and a transport protocol.
    /// </summary>
    /// <param name="content">The UTF-8 string to assign to this packet.</param>
    /// <param name="transport">The protocol to use when transmitting this packet.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the content exceeds <see cref="DynamicSize"/> bytes.
    /// </exception>
    public void Initialize(string content, ProtocolType transport = ProtocolType.TCP)
    {
        string text = content ?? string.Empty;
        int byteCount = Encoding.UTF8.GetByteCount(text);

        if (byteCount > DynamicSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(content),
                $"Content exceeds maximum size of {DynamicSize} UTF-8 bytes.");
        }

        this.Content = text;
        this.Protocol = transport;
    }

    /// <inheritdoc/>
    public override void ResetForPool()
    {
        base.ResetForPool();

        // Reset local state
        this.Content = string.Empty;
    }

    /// <inheritdoc/>
    public override string ToString()
        => $"TEXT512(OpCode={this.OpCode}, Length={this.Length}, Flags={this.Flags}, " +
           $"Priority={this.Priority}, Protocol={this.Protocol}, ContentBytes={Encoding.UTF8.GetByteCount(this.Content)})";
}
