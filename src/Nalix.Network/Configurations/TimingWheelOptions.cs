// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Shared.Configuration.Binding;

namespace Nalix.Network.Configurations;

/// <summary>
/// Represents configuration settings for idle connection timeout in the network layer.
/// Defines how long an inactive connection can stay open before being automatically closed.
/// </summary>
public sealed class TimingWheelOptions : ConfigurationLoader
{
    #region Properties

    /// <summary>
    /// Gets or sets the idle timeout for TCP connections in milliseconds.
    /// If a connection is inactive longer than this value, it will be closed automatically.
    /// Default value is 60000 (60 seconds).
    /// </summary>
    public System.Int32 TcpIdleTimeoutMs { get; set; } = 60_000;

    /// <summary>
    /// Gets or sets the idle timeout for UDP connections in milliseconds.
    /// Default value is 30000 (30 seconds).
    /// </summary>
    public System.Int32 UdpIdleTimeoutMs { get; set; } = 30_000;

    /// <summary>
    /// Gets or sets the precision of the idle check tick in milliseconds.
    /// Lower values mean more frequent checks but slightly higher CPU usage.
    /// Default is 1000 (1 second).
    /// </summary>
    public System.Int32 TickDurationMs { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the size of the timing wheel (number of buckets).
    /// Higher values reduce collisions but use a bit more memory.
    /// Default is 512.
    /// </summary>
    public System.Int32 WheelSize { get; set; } = 512;

    #endregion Properties
}
