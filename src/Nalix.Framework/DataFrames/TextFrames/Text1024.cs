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
/// Represents a UTF-8 text packet supporting up to 1024 bytes.
/// Implemented using PacketBase for consistent pooling and serialization.
/// </summary>
[SerializePackable(SerializeLayout.Explicit)]
[ExcludeFromCodeCoverage]
[DebuggerDisplay("TEXT1024 OpCode={OpCode}, Length={Length}, Flags={Flags}")]
public sealed class Text1024 : PacketBase<Text1024>
{
    /// <summary>
    /// Maximum UTF-8 byte count allowed inside <see cref="Content"/>.
    /// </summary>
    public const int DynamicSize = 1024;

    /// <summary>
    /// The UTF-8 text content carried by this packet.
    /// </summary>
    [SerializeDynamicSize(DynamicSize)]
    [SerializeOrder(PacketHeaderOffset.Region)]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Creates a new <see cref="Text1024"/> instance with default header values.
    /// </summary>
    public Text1024() : base()
    {
        this.Protocol = ProtocolType.NONE;
        this.Flags = PacketFlags.NONE;
        this.Priority = PacketPriority.NONE;
        this.OpCode = PacketConstants.OpcodeDefault;
        this.Content = string.Empty;
    }

    /// <summary>
    /// Initializes the packet with the specified text and transport protocol.
    /// </summary>
    /// <param name="content">The UTF-8 string content.</param>
    /// <param name="transport">The transport protocol used for sending this frame.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="content"/> exceeds <see cref="DynamicSize"/> bytes.
    /// </exception>
    public void Initialize(string content, ProtocolType transport = ProtocolType.TCP)
    {
        string text = content ?? string.Empty;
        int size = Encoding.UTF8.GetByteCount(text);

        if (size > DynamicSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(content),
                $"Text1024 supports a maximum of {DynamicSize} UTF-8 bytes. Actual: {size}.");
        }

        this.Content = text;
        this.Protocol = transport;
    }

    /// <inheritdoc/>
    public override void ResetForPool()
    {
        // Reset header fields from PacketBase
        base.ResetForPool();

        // Reset local fields
        this.Content = string.Empty;
    }

    /// <inheritdoc/>
    public override string ToString()
        => $"TEXT1024(OpCode={this.OpCode}, Length={this.Length}, Flags={this.Flags}, " +
           $"Priority={this.Priority}, Protocol={this.Protocol}, Bytes={Encoding.UTF8.GetByteCount(this.Content)})";
}
