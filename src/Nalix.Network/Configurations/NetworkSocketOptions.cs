// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Shared;
using Nalix.Framework.Configuration.Binding;

namespace Nalix.Network.Configurations;

/// <summary>
/// Represents network configuration settings for socket and TCP connections.
/// </summary>
[IniComment("Network socket configuration — controls port, buffering, concurrency, and socket behavior")]
public sealed class NetworkSocketOptions : ConfigurationLoader
{
    #region Fields

    private System.UInt16 _port = 57206;

    #endregion Fields

    #region Constants

    internal const System.Int32 True = 1;
    internal const System.Int32 False = 0;

    #endregion Constants

    #region Properties

    /// <summary>
    /// Gets or sets the port number for the network connection.
    /// </summary>
    [IniComment("TCP port to listen on (1–65535, default 57206)")]
    [System.ComponentModel.DataAnnotations.Range(1, 65535, ErrorMessage = "Port must be between 1 and 65535.")]
    public System.UInt16 Port
    {
        get => this._port;
        set
        {
            if (value is < 1 or > 65535)
            {
                throw new System.ArgumentOutOfRangeException(nameof(value), "Port must be between 1 and 65535.");
            }

            this._port = value;
        }
    }

    /// <summary>
    /// Gets or sets the maximum length of the pending connections queue.
    /// </summary>
    [IniComment("Maximum pending connection queue length (1–65535)")]
    [System.ComponentModel.DataAnnotations.Range(1, 65535, ErrorMessage = "Backlog must be between 1 and 65535.")]
    public System.Int32 Backlog { get; set; } = 512;

    /// <summary>
    /// Gets or sets a value indicating whether the idle timeout mechanism is enabled.
    /// </summary>
    [IniComment("Enable idle connection timeout enforcement")]
    public System.Boolean EnableTimeout { get; set; } = true;

    /// <summary>
    /// Indicates whether to use IPv6 instead of IPv4.
    /// </summary>
    [IniComment("Listen on IPv6 instead of IPv4")]
    public System.Boolean EnableIPv6 { get; set; } = false;

    /// <summary>
    /// Gets or sets whether Nagle's algorithm is disabled (low-latency mode).
    /// </summary>
    [IniComment("Disable Nagle's algorithm for lower latency (recommended: true)")]
    public System.Boolean NoDelay { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of parallel connections.
    /// </summary>
    [IniComment("Maximum simultaneous parallel connections (minimum 1)")]
    [System.ComponentModel.DataAnnotations.Range(1, System.Int32.MaxValue, ErrorMessage = "MaxParallel must be at least 1.")]
    public System.Int32 MaxParallel { get; set; } = 5;

    /// <summary>
    /// Gets or sets the buffer size for both sending and receiving data.
    /// </summary>
    [IniComment("Send and receive buffer size in bytes (64–10,485,760)")]
    [System.ComponentModel.DataAnnotations.Range(64, 10_485_760, ErrorMessage = "BufferSize must be between 512 and 10MiB (10,485,760 bytes).")]
    public System.Int32 BufferSize { get; set; } = 4 * 1024;

    /// <summary>
    /// Gets or sets a value indicating whether TCP Keep-Alive is enabled.
    /// </summary>
    [IniComment("Enable TCP Keep-Alive probes to detect dead connections")]
    public System.Boolean KeepAlive { get; set; } = false;

    /// <summary>
    /// Gets or sets whether the socket can reuse an address in TIME_WAIT state.
    /// </summary>
    [IniComment("Allow reuse of a local address in TIME_WAIT state (recommended: true)")]
    public System.Boolean ReuseAddress { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of concurrent groups for socket operations.
    /// </summary>
    [IniComment("Maximum concurrent socket operation groups (1–1024)")]
    [System.ComponentModel.DataAnnotations.Range(1, 1024, ErrorMessage = "MaxGroupConcurrency must be between 1 and 1024.")]
    public System.Int32 MaxGroupConcurrency { get; set; } = 8;

    /// <summary>
    /// Tunes the thread pool settings for optimal network performance.
    /// </summary>
    [IniComment("Apply thread pool tuning optimized for high-throughput network workloads")]
    public System.Boolean TuneThreadPool { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether the DualMode feature is enabled (for IPv6 sockets).
    /// </summary>
    [IniComment("Enable DualMode to support both IPv4 and IPv6 connections on the same socket (defaults: true for IPv6)")]
    public System.Boolean DualMode { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether only one socket can bind to the address (exclusive use).
    /// </summary>
    [IniComment("Specify whether ExclusiveAddressUse is enabled (default: true unless ReuseAddress is true)")]
    public System.Boolean ExclusiveAddressUse { get; set; } = true;

    /// <summary>
    /// Maximum accepted connections that may queue in the channel while the consumer
    /// thread is busy.
    /// <para>
    /// Tune to roughly <c>2 × burst rate × ProcessConnection latency (ms)</c>.
    /// Default 128 matches the typical TCP backlog.
    /// </para>
    /// </summary>
    [IniComment("Maximum accepted connections that may queue in the channel while the consumer thread is busy (tune to ~2 × burst rate × ProcessConnection latency in ms, default 128)")]
    [System.ComponentModel.DataAnnotations.Range(1, System.Int32.MaxValue, ErrorMessage = "ProcessChannelCapacity must be at least 1.")]
    public System.Int32 ProcessChannelCapacity { get; set; } = 128;

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