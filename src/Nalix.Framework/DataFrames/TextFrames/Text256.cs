// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Text;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Serialization;

namespace Nalix.Framework.DataFrames.TextFrames;

/// <summary>
/// A UTF-8 text packet implemented using PacketBase for consistency.
/// Supports dynamic-size strings up to 256 bytes.
/// </summary>
[SerializePackable(SerializeLayout.Explicit)]
public sealed class Text256 : PacketBase<Text256>
{
    /// <summary>
    /// Maximum UTF-8 byte length of Content.
    /// </summary>
    public const int DynamicSize = 256;

    /// <summary>
    /// UTF-8 string content.
    /// </summary>
    [SerializeDynamicSize(DynamicSize)]
    [SerializeOrder(PacketHeaderOffset.Region)]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Initializes a new empty text frame.
    /// </summary>
    public Text256() : base()
    {
        this.Content = string.Empty;
        this.Protocol = ProtocolType.NONE;
        this.OpCode = PacketConstants.OpcodeDefault;
    }

    /// <summary>
    /// Initializes with content and optional protocol.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="content"/> exceeds <see cref="DynamicSize"/> UTF-8 bytes.</exception>
    public void Initialize(string content, ProtocolType protocol = ProtocolType.TCP)
    {
        if (content is null)
        {
            this.Content = string.Empty;
            this.Protocol = protocol;
            return;
        }

        if (Encoding.UTF8.GetByteCount(content) > DynamicSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(content), $"TextFrame supports at most {DynamicSize} UTF-8 bytes.");
        }

        this.Content = content;
        this.Protocol = protocol;
    }

    /// <inheritdoc/>
    public override void ResetForPool()
    {
        base.ResetForPool();   // Reset header + metadata
        this.Content = string.Empty;
    }

    /// <inheritdoc/>
    public override string ToString()
        => $"TEXT(OpCode={this.OpCode}, Len={this.Length}, Proto={this.Protocol}, ContentBytes={Encoding.UTF8.GetByteCount(this.Content)})";
}
