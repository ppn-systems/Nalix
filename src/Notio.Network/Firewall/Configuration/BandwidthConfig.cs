using Notio.Shared.Configuration;
using System;
using System.ComponentModel.DataAnnotations;

namespace Notio.Network.Firewall.Configuration;

/// <summary>
/// Represents the configuration settings for managing bandwidth limits.
/// </summary>
public sealed class BandwidthConfig : ConfiguredBinder
{
    /// <summary>
    /// Gets or sets the maximum upload speed in bytes per second.
    /// The value must be greater than or equal to 1024 bytes per second.
    /// </summary>
    [Range(1024, long.MaxValue)]
    public long MaxUploadBytesPerSecond { get; set; } = 1024 * 1024; // 1MB/s

    /// <summary>
    /// Gets or sets the maximum download speed in bytes per second.
    /// The value must be greater than or equal to 1024 bytes per second.
    /// </summary>
    [Range(1024, long.MaxValue)]
    public long MaxDownloadBytesPerSecond { get; set; } = 1024 * 1024; // 1MB/s

    /// <summary>
    /// Gets or sets the maximum burst size for uploads in bytes.
    /// The value must be greater than or equal to 1024 bytes.
    /// </summary>
    [Range(1024, long.MaxValue)]
    public int UploadBurstSize { get; set; } = 1024 * 1024 * 2; // 2MB burst

    /// <summary>
    /// Gets or sets the maximum burst size for downloads in bytes.
    /// The value must be greater than or equal to 1024 bytes.
    /// </summary>
    [Range(1024, long.MaxValue)]
    public int DownloadBurstSize { get; set; } = 1024 * 1024 * 2; // 2MB burst

    /// <summary>
    /// Gets or sets the interval in seconds for resetting bandwidth statistics.
    /// The value must be between 1 and 3600 seconds (1 hour).
    /// </summary>
    [Range(1, 3600)]
    public int BandwidthResetIntervalSeconds { get; set; } = 60;
}
