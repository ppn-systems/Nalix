// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions;
using Nalix.Environment.Configuration.Binding;

namespace Nalix.Network.Options;

/// <summary>
/// Represents network configuration settings for WebSocket connections.
/// </summary>
[IniComment("Network WebSocket configuration — controls endpoint, subprotocol, and behavior")]
public sealed partial class NetworkWebSocketOptions : ConfigurationLoader, IValidatableConfiguration
{
    /// <summary>
    /// Gets or sets the port number for the WebSocket connection.
    /// </summary>
    [IniComment("WebSocket port to listen on (1–65535, default 57207)")]
    [System.ComponentModel.DataAnnotations.Range(1, 65535, ErrorMessage = "Port must be between 1 and 65535.")]
    public ushort Port { get; set; } = 57207;

    /// <summary>
    /// Gets or sets the WebSocket path.
    /// </summary>
    [IniComment("WebSocket endpoint path (default /ws/)")]
    public string Path { get; set; } = "/ws/";

    /// <summary>
    /// Gets or sets the host to bind the listener to (e.g., *, +, localhost).
    /// </summary>
    [IniComment("Host address to bind to (default *)")]
    public string Host { get; set; } = "*";

    /// <summary>
    /// Gets or sets the subprotocol to negotiate.
    /// </summary>
    [IniComment("WebSocket subprotocol identifier (default nalix.v1)")]
    public string SubProtocol { get; set; } = "nalix.v1";

    /// <summary>
    /// Gets or sets a value indicating whether the idle timeout mechanism is enabled.
    /// </summary>
    [IniComment("Enable idle connection timeout enforcement")]
    public bool EnableTimeout { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum time in milliseconds to wait for the process channel to drain
    /// gracefully during shutdown before forceful termination.
    /// </summary>
    [IniComment("Maximum time in milliseconds to wait for the process channel to drain gracefully during shutdown (default: 5000)")]
    [System.ComponentModel.DataAnnotations.Range(0, 60000, ErrorMessage = "ProcessChannelDrainTimeout must be between 0 and 60000 ms.")]
    public int ProcessChannelDrainTimeout { get; set; } = 5000;

    /// <summary>
    /// Maximum accepted connections that may queue in the channel while the consumer
    /// thread is busy.
    /// </summary>
    [IniComment("Maximum accepted connections that may queue in the channel while the consumer thread is busy (default 256)")]
    [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue, ErrorMessage = "ProcessChannelCapacity must be at least 1.")]
    public int ProcessChannelCapacity { get; set; } = 256;

    /// <summary>
    /// Maximum inbound WebSocket message size in bytes.
    /// </summary>
    [IniComment("Maximum inbound WebSocket message size in bytes (default 1048576)")]
    [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue, ErrorMessage = "MaxMessageSize must be positive.")]
    public int MaxMessageSize { get; set; } = 1_048_576;

    /// <summary>
    /// Number of concurrent accept workers to spawn for handling new WebSocket connections.
    /// </summary>
    [IniComment("Number of concurrent accept workers (default 1)")]
    [System.ComponentModel.DataAnnotations.Range(1, 1024, ErrorMessage = "MaxParallel must be between 1 and 1024.")]
    public int MaxParallel { get; set; } = 1;

    /// <summary>
    /// Validates the configuration options and throws an exception if validation fails.
    /// </summary>
    /// <exception cref="System.ComponentModel.DataAnnotations.ValidationException">
    /// Thrown when one or more validation attributes fail.
    /// </exception>
    public void Validate() => this.ValidateDataAnnotations();
}
