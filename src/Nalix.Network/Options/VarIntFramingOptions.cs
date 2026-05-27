// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Environment.Configuration.Binding;

namespace Nalix.Network.Options;

/// <summary>
/// Configuration options for VarInt framing (Minecraft-style).
/// </summary>
public sealed partial class VarIntFramingOptions : ConfigurationLoader
{
    /// <summary>
    /// The maximum allowed payload size for a VarInt-framed packet.
    /// Connections sending packets larger than this will be disconnected.
    /// Default is 2 MB (2097152 bytes).
    /// </summary>
    public int MaxPayloadSize { get; set; } = 2 * 1024 * 1024;
}
