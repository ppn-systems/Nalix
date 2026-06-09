// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions;
using Nalix.Abstractions.Validation;
using Nalix.Environment.Configuration.Binding;

namespace Nalix.Network.Options;

/// <summary>
/// Represents configuration options for Proxy Protocol V1/V2 parsing.
/// </summary>
[IniComment("Proxy Protocol configuration — handling real IP extraction behind load balancers")]
public sealed partial class ProxyProtocolOptions : ConfigurationLoader, IValidatableConfiguration
{
    /// <summary>
    /// Enable Proxy Protocol V1/V2 header parsing to extract the real client IP.
    /// Must be true when the server sits behind HAProxy, AWS NLB, or any proxy
    /// that injects a PROXY header.
    /// </summary>
    [IniComment("Enable Proxy Protocol V1/V2 parsing to obtain the real client IP (default: false)")]
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// When true, only connections originating from IPs listed in the Trusted Proxies list
    /// are allowed to perform a Proxy Protocol header read. All other connections are dropped.
    /// </summary>
    [IniComment("Drop Proxy Protocol headers from untrusted physical IPs (default: false)")]
    public bool RequireTrustedProxy { get; set; } = false;

    /// <summary>
    /// Maximum milliseconds to wait for the PROXY header to arrive after TCP accept.
    /// Connections that do not deliver a complete header within this window are dropped.
    /// Tune to roughly RTT_max + 50ms. Default 2000ms.
    /// </summary>
    [IniComment("Timeout in ms for the PROXY header read (default: 2000)")]
    [ValueRange(100, 30000)]
    public int HeaderTimeoutMs { get; set; } = 2000;

    /// <summary>
    /// Maximum number of in-flight connections waiting for a PROXY header.
    /// This prevents DDoS attacks that exhaust server memory by holding connections open.
    /// </summary>
    [IniComment("Maximum concurrent connections waiting for a Proxy Protocol header (default: 1024)")]
    [ValueRange(1, 100_000)]
    public int MaxPendingProxyConnections { get; set; } = 1024;

    /// <summary>
    /// Validates the configuration options and throws an exception if validation fails.
    /// </summary>
    /// <exception cref="Nalix.Abstractions.Validation.ValidationException">
    /// Thrown when one or more validation attributes fail.
    /// </exception>
    public void Validate() => this.ValidateDataAnnotations();
}
