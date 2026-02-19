// Copyright (c) 2026 PPN Corporation. All rights reserved.

using Nalix.Framework.Configuration.Binding;

namespace Nalix.Network.Configurations;

/// <summary>
/// Represents configuration settings for idle connection timeout in the network layer.
/// Defines how long an inactive connection can stay open before being automatically closed.
/// </summary>
public sealed class TimingWheelOptions : ConfigurationLoader
{
    /// <summary>
    /// Gets or sets the size of the timing wheel (number of buckets).
    /// Higher values reduce collisions but use a bit more memory.
    /// Default is 512.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Range(1, System.Int32.MaxValue)]
    public System.Int32 BucketCount { get; set; } = 512;

    /// <summary>
    /// Gets or sets the precision of the idle check tick in milliseconds.
    /// Lower values mean more frequent checks but slightly higher CPU usage.
    /// Default is 1000 (1 second).
    /// </summary>
    [System.ComponentModel.DataAnnotations.Range(1, System.Int32.MaxValue)]
    public System.Int32 TickDuration { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the idle timeout for TCP connections in milliseconds.
    /// If a connection is inactive longer than this value, it will be closed automatically.
    /// Default value is 60000 (60 seconds).
    /// </summary>
    [System.ComponentModel.DataAnnotations.Range(1, System.Int32.MaxValue)]
    public System.Int32 TcpIdleTimeout { get; set; } = 60_000;

    /// <summary>
    /// Gets or sets the idle timeout for UDP connections in milliseconds.
    /// Default value is 30000 (30 seconds).
    /// </summary>
    [System.ComponentModel.DataAnnotations.Range(1, System.Int32.MaxValue)]
    public System.Int32 UdpIdleTimeout { get; set; } = 30_000;

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