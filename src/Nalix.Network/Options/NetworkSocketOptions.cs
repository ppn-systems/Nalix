// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Abstractions;
using Nalix.Environment.Configuration.Binding;

namespace Nalix.Network.Options;

/// <summary>
/// Represents network configuration settings for socket and TCP connections.
/// </summary>
[IniComment("Network socket configuration — controls port, buffering, concurrency, and socket behavior")]
public sealed class NetworkSocketOptions : ConfigurationLoader
{
    #region Constants

    internal const int True = 1;
    internal const int False = 0;

    #endregion Constants

    #region Properties

    /// <summary>
    /// Gets or sets the port number for the network connection.
    /// </summary>
    /// <exception cref="System.ArgumentOutOfRangeException"></exception>
    [IniComment("TCP port to listen on (1–65535, default 57206)")]
    [System.ComponentModel.DataAnnotations.Range(1, 65535, ErrorMessage = "Port must be between 1 and 65535.")]
    public ushort Port
    {
        get;
        set
        {
            if (value < 1)
            {
                throw new System.ArgumentOutOfRangeException(nameof(value), "Port must be at least 1 (0 is not allowed).");
            }

            field = value;
        }
    } = 57206;

    /// <summary>
    /// Gets or sets the maximum length of the pending connections queue.
    /// </summary>
    [IniComment("Maximum pending connection queue length (1–65535)")]
    [System.ComponentModel.DataAnnotations.Range(1, 65535, ErrorMessage = "Backlog must be between 1 and 65535.")]
    public int Backlog { get; set; } = 512;

    /// <summary>
    /// Gets or sets a value indicating whether the idle timeout mechanism is enabled.
    /// </summary>
    [IniComment("Enable idle connection timeout enforcement")]
    public bool EnableTimeout { get; set; } = true;

    /// <summary>
    /// Indicates whether to use IPv6 instead of IPv4.
    /// </summary>
    [IniComment("Listen on IPv6 instead of IPv4")]
    public bool EnableIPv6 { get; set; } = false;

    /// <summary>
    /// Gets or sets whether Nagle's algorithm is disabled (low-latency mode).
    /// </summary>
    [IniComment("Disable Nagle's algorithm for lower latency (recommended: true)")]
    public bool NoDelay { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of parallel connections.
    /// </summary>
    [IniComment("Maximum simultaneous parallel listeners/acceptors (1–1024, default 5)")]
    [System.ComponentModel.DataAnnotations.Range(1, 1024, ErrorMessage = "MaxParallel must be between 1 and 1024.")]
    public int MaxParallel { get; set; } = 5;

    /// <summary>
    /// Gets or sets the maximum number of parallel connections.
    /// </summary>
    [IniComment("Maximum simultaneous parallel listeners/acceptors (1–1024, default 2)")]
    [System.ComponentModel.DataAnnotations.Range(1, 1024, ErrorMessage = "MaxParallel UDP must be between 1 and 1024.")]
    public int MaxParallelUDP { get; set; } = 2;

    /// <summary>
    /// Gets or sets the buffer size for both sending and receiving data.
    /// </summary>
    [IniComment("Send and receive buffer size in bytes (1024–10,485,760)")]
    [System.ComponentModel.DataAnnotations.Range(2048, 10_485_760, ErrorMessage = "BufferSize must be between 2048B and 10MiB (10,485,760 bytes).")]
    public int BufferSize { get; set; } = 65536;

    /// <summary>
    /// Gets or sets a value indicating whether TCP Keep-Alive is enabled.
    /// </summary>
    [IniComment("Enable TCP Keep-Alive probes to detect dead connections")]
    public bool KeepAlive { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the socket can reuse an address in TIME_WAIT state.
    /// </summary>
    [IniComment("Allow reuse of a local address in TIME_WAIT state (recommended: true)")]
    public bool ReuseAddress { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of concurrent groups for socket operations.
    /// </summary>
    [IniComment("Maximum concurrent socket operation groups (1–1024)")]
    [System.ComponentModel.DataAnnotations.Range(1, 1024, ErrorMessage = "MaxGroupConcurrency must be between 1 and 1024.")]
    public int MaxGroupConcurrency { get; set; } = 8;

    /// <summary>
    /// Gets or sets a value indicating whether the DualMode feature is enabled (for IPv6 sockets).
    /// </summary>
    [IniComment("Enable DualMode to support both IPv4 and IPv6 connections on the same socket (defaults: true for IPv6)")]
    public bool DualMode { get; set; } = true;

    /// <summary>
    /// Maximum accepted connections that may queue in the channel while the consumer
    /// thread is busy.
    /// <para>
    /// Tune to roughly <c>2 × burst rate × ProcessConnection latency (ms)</c>.
    /// Default 256 matches the typical TCP backlog.
    /// </para>
    /// </summary>
    [IniComment("Maximum accepted connections that may queue in the channel while the consumer thread is busy (tune to ~2 × burst rate × ProcessConnection latency in ms, default 128)")]
    [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue, ErrorMessage = "ProcessChannelCapacity must be at least 1.")]
    public int ProcessChannelCapacity { get; set; } = 256;

    #endregion Properties

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
