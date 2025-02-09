using Notio.Shared.Configuration;
using System;
using System.ComponentModel.DataAnnotations;

namespace Notio.Network.Firewall.Configuration;

public sealed class BandwidthConfig : ConfiguredBinder
{
    [Range(1024, long.MaxValue)]
    public long MaxUploadBytesPerSecond { get; set; } = 1024 * 1024; // 1MB/s

    [Range(1024, long.MaxValue)]
    public long MaxDownloadBytesPerSecond { get; set; } = 1024 * 1024; // 1MB/s

    [Range(1024, long.MaxValue)]
    public int UploadBurstSize { get; set; } = 1024 * 1024 * 2; // 2MB burst

    [Range(1024, long.MaxValue)]
    public int DownloadBurstSize { get; set; } = 1024 * 1024 * 2; // 2MB burst

    [Range(1, 3600)]
    public int BandwidthResetIntervalSeconds { get; set; } = 60;
}