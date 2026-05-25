// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions;
using Nalix.Environment.Configuration.Binding;

namespace Nalix.Network.Options;

/// <summary>
/// Represents configuration options for Proxy Protocol V1/V2 parsing.
/// </summary>
[IniComment("Proxy Protocol configuration — handling real IP extraction behind load balancers")]
public sealed partial class ProxyProtocolOptions : ConfigurationLoader
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
    [System.ComponentModel.DataAnnotations.Range(100, 30000, ErrorMessage = "HeaderTimeoutMs must be between 100 and 30000.")]
    public int HeaderTimeoutMs { get; set; } = 2000;

    /// <summary>
    /// Validates the configuration options and throws an exception if validation fails.
    /// </summary>
    /// <exception cref="System.ComponentModel.DataAnnotations.ValidationException">
    /// Thrown when one or more validation attributes fail.
    /// </exception>
    public void Validate()
    {
        System.ComponentModel.DataAnnotations.ValidationContext context = new(this);
        System.ComponentModel.DataAnnotations.Validator.ValidateObject(this, context, validateAllProperties: true);
    }
}
