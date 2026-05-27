// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.ComponentModel.DataAnnotations;
using Nalix.Abstractions;
using Nalix.Environment.Configuration.Binding;

namespace Nalix.SDK.Options;

/// <summary>
/// Client-side WebSocket configuration used by <c>WebSocketSession</c>.
/// </summary>
[IniComment("Client WebSocket transport configuration")]
public sealed partial class WebSocketTransportOptions : ConfigurationLoader, IValidatableConfiguration
{
    /// <summary>
    /// Gets or sets the WebSocket endpoint path.
    /// </summary>
    [IniComment("WebSocket endpoint path (default /ws/)")]
    public string Path { get; set; } = "/ws/";

    /// <summary>
    /// Gets or sets the WebSocket subprotocol to request.
    /// </summary>
    [IniComment("WebSocket subprotocol identifier (default nalix.v1)")]
    public string SubProtocol { get; set; } = "nalix.v1";

    /// <summary>
    /// Gets or sets a value indicating whether WebSocket transport should use TLS.
    /// </summary>
    [IniComment("Use secure WebSocket transport (wss://)")]
    public bool UseTls { get; set; }

    /// <summary>
    /// Gets or sets the maximum inbound WebSocket message size in bytes.
    /// </summary>
    [IniComment("Maximum inbound WebSocket message size in bytes")]
    [Range(1, int.MaxValue, ErrorMessage = "MaxMessageSize must be positive.")]
    public int MaxMessageSize { get; set; } = 1_048_576;

    /// <summary>
    /// Validates the configuration options.
    /// </summary>
    public void Validate() => this.ValidateDataAnnotations();
}
