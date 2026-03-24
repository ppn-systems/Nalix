// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Networking.Packets;

namespace Nalix.SDK.Tools.Models;

/// <summary>
/// Represents the remote endpoint configuration used by the tool.
/// </summary>
public sealed class ConnectionSettings
{
    /// <summary>
    /// Gets or sets the target host name or IP address.
    /// </summary>
    public string Host { get; set; } = "127.0.0.1";

    /// <summary>
    /// Gets or sets the target port.
    /// </summary>
    public ushort Port { get; set; } = 57206;

    /// <summary>
    /// Gets or sets the transport protocol (TCP/UDP).
    /// </summary>
    public PacketFlags Transport { get; set; } = PacketFlags.RELIABLE;

    /// <summary>
    /// Gets or sets the UDP session token (7-byte Snowflake hex).
    /// </summary>
    public string SessionToken { get; set; } = string.Empty;
}
