// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions;
using Nalix.Environment.Configuration.Binding;

namespace Nalix.Network.Options;

/// <summary>
/// Configuration for L7 HTTP Forwarded Headers (e.g., Cloudflare, Nginx Reverse Proxy).
/// Used by the WebSocket listener to extract the real IP address of clients.
/// </summary>
[IniComment("Configuration for L7 HTTP Forwarded Headers (CF-Connecting-IP, X-Forwarded-For)")]
public sealed partial class ForwardedHeadersOptions : ConfigurationLoader
{
    /// <summary>
    /// Enable reading of HTTP Proxy headers like CF-Connecting-IP and X-Forwarded-For
    /// to obtain the real client IP.
    /// </summary>
    [IniComment("Enable parsing HTTP Proxy headers (CF-Connecting-IP, X-Forwarded-For) (default: false)")]
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// When true, only connections originating from IPs listed in the Trusted Proxies list
    /// are allowed to forward the IP via HTTP Headers. All other connections are rejected.
    /// </summary>
    [IniComment("Drop connections if HTTP headers are sent from untrusted physical IPs (default: false)")]
    public bool RequireTrustedProxy { get; set; } = false;
}
