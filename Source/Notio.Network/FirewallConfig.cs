using Notio.Network.Firewall.Enums;
using Notio.Shared.Configuration;
using System.ComponentModel.DataAnnotations;

namespace Notio.Network;

public class FirewallConfig : ConfigContainer
{
    // Cấu hình giới hạn băng thông
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

    // Cấu hình giới hạn kết nối
    [Range(1, 1000)]
    public int MaxConnectionsPerIpAddress { get; set; } = 100;

    // Cấu hình Rate Limit
    [Range(1, 1000)]
    public int MaxAllowedRequests { get; set; } = 100;

    [Range(1, 3600)]
    public int LockoutDurationSeconds { get; set; } = 300;

    [Range(1000, int.MaxValue)]
    public int TimeWindowInMilliseconds { get; set; } = 60000; // 1 phút

    // Cấu hình Chung
    public bool EnableLogging { get; set; } = true;

    public bool EnableMetrics { get; set; } = true;

    // Cấu hình mức độ giới hạn
    [ConfigIgnore]
    public BandwidthLimit BandwidthLimit { get; set; } = BandwidthLimit.Medium;

    [ConfigIgnore]
    public ConnectionLimit ConnectionLimit { get; set; } = ConnectionLimit.Medium;

    [ConfigIgnore]
    public RequestLimit RequestLimit { get; set; } = RequestLimit.Medium;

    // Phương thức để áp dụng cấu hình theo mức độ
    public void ApplyLimits()
    {
        this.ApplyRequestLimit();
        this.ApplyBandwidthLimit();
        this.ApplyConnectionLimit();
    }

    private void ApplyRequestLimit()
    {
        switch (RequestLimit)
        {
            case RequestLimit.Low:
                MaxAllowedRequests = 50;
                LockoutDurationSeconds = 600;
                TimeWindowInMilliseconds = 30000;
                break;

            case RequestLimit.Medium:
                MaxAllowedRequests = 100;
                LockoutDurationSeconds = 300;
                TimeWindowInMilliseconds = 60000;
                break;

            case RequestLimit.High:
                MaxAllowedRequests = 500;
                LockoutDurationSeconds = 150;
                TimeWindowInMilliseconds = 120000;
                break;

            case RequestLimit.Unlimited:
                MaxAllowedRequests = 1000;
                LockoutDurationSeconds = 60;
                TimeWindowInMilliseconds = 300000;
                break;
        }
    }

    private void ApplyBandwidthLimit()
    {
        switch (BandwidthLimit)
        {
            case BandwidthLimit.Low:
                MaxUploadBytesPerSecond = 512 * 1024; // 512KB/s
                MaxDownloadBytesPerSecond = 512 * 1024;
                UploadBurstSize = 1024 * 1024; // 1MB burst
                DownloadBurstSize = 1024 * 1024;
                break;

            case BandwidthLimit.Medium:
                MaxUploadBytesPerSecond = 1024 * 1024; // 1MB/s
                MaxDownloadBytesPerSecond = 1024 * 1024;
                UploadBurstSize = 2 * 1024 * 1024; // 2MB burst
                DownloadBurstSize = 2 * 1024 * 1024;
                break;

            case BandwidthLimit.High:
                MaxUploadBytesPerSecond = 5 * 1024 * 1024; // 5MB/s
                MaxDownloadBytesPerSecond = 5 * 1024 * 1024;
                UploadBurstSize = 10 * 1024 * 1024; // 10MB burst
                DownloadBurstSize = 10 * 1024 * 1024;
                break;

            case BandwidthLimit.Unlimited:
                MaxUploadBytesPerSecond = long.MaxValue;
                MaxDownloadBytesPerSecond = long.MaxValue;
                UploadBurstSize = int.MaxValue;
                DownloadBurstSize = int.MaxValue;
                break;
        }
    }

    private void ApplyConnectionLimit()
    {
        switch (ConnectionLimit)
        {
            case ConnectionLimit.Low:
                MaxConnectionsPerIpAddress = 20;
                break;

            case ConnectionLimit.Medium:
                MaxConnectionsPerIpAddress = 100;
                break;

            case ConnectionLimit.High:
                MaxConnectionsPerIpAddress = 500;
                break;

            case ConnectionLimit.Unlimited:
                MaxConnectionsPerIpAddress = 1000;
                break;
        }
    }

    // Helper method để validate cấu hình
    public bool Validate(out string error)
    {
        try
        {
            Validator.ValidateObject(this, new ValidationContext(this), true);
            error = string.Empty;
            return true;
        }
        catch (ValidationException ex)
        {
            error = ex.Message;
            return false;
        }
    }
}