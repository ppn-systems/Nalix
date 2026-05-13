// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.ComponentModel.DataAnnotations;
using Nalix.Abstractions;
using Nalix.Environment.Configuration.Binding;

namespace Nalix.Codec.Options;

/// <summary>
/// Configuration for sequence number validation windows (anti-replay + reordering support).
/// </summary>
[IniComment("Sequence counter configuration - controls replay protection and packet reordering tolerance")]
public sealed partial class SequenceOptions : ConfigurationLoader
{
    /// <summary>
    /// Gets or sets the reordering window size for TCP connections.
    /// TCP usually has very few out-of-order packets, so small value is recommended.
    /// </summary>
    [IniComment("Reordering window for TCP (default: 0). Small value is sufficient and more secure.")]
    [Range(0, 256)]
    public uint TcpWindow { get; set; } = 0;

    /// <summary>
    /// Gets or sets the reordering window size for UDP connections.
    /// UDP can have more reordering/loss, so larger window is recommended.
    /// </summary>
    [IniComment("Reordering window for UDP (default: 128). Increase if you have high packet loss/jitter.")]
    [Range(0, 1024)]
    public uint UdpWindow { get; set; } = 128;

    /// <summary>
    /// Validates the configuration options.
    /// </summary>
    public void Validate()
    {
        if (this.TcpWindow > 256)
        {
            throw new ValidationException("TcpWindow should not exceed 256 for security reasons (TCP has low reordering).");
        }

        if (this.UdpWindow > 1024)
        {
            throw new ValidationException("UdpWindow should not exceed 1024. Larger values weaken replay protection.");
        }

        if (this.UdpWindow < this.TcpWindow)
        {
            throw new ValidationException("UdpWindow should be greater than or equal to TcpWindow.");
        }
    }
}
