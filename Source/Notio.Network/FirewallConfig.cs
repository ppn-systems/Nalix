using Notio.Network.Firewall.Configuration;
using Notio.Network.Firewall.Enums;
using Notio.Shared.Configuration;
using System.ComponentModel.DataAnnotations;

namespace Notio.Network;

public sealed class FirewallConfig : ConfiguredBinder
{
    // Cấu hình Chung
    public bool EnableLogging { get; set; } = true;

    public bool EnableMetrics { get; set; } = true;

    // Cấu hình giới hạn băng thông
    [ConfiguredIgnore]
    public BandwidthConfig Bandwidth { get; set; } = new BandwidthConfig();

    // Cấu hình giới hạn kết nối
    [ConfiguredIgnore]
    public ConnectionConfig Connection { get; set; } = new ConnectionConfig();

    // Cấu hình Rate Limit
    [ConfiguredIgnore]
    public RateLimitConfig RateLimit { get; set; } = new RateLimitConfig();

    // Cấu hình mức độ giới hạn
    [ConfiguredIgnore]
    public BandwidthLimit BandwidthLimit { get; set; } = BandwidthLimit.Medium;

    [ConfiguredIgnore]
    public ConnectionLimit ConnectionLimit { get; set; } = ConnectionLimit.Medium;

    [ConfiguredIgnore]
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
                RateLimit.MaxAllowedRequests = 50;
                RateLimit.LockoutDurationSeconds = 600;
                RateLimit.TimeWindowInMilliseconds = 30000;
                break;

            case RequestLimit.Medium:
                RateLimit.MaxAllowedRequests = 100;
                RateLimit.LockoutDurationSeconds = 300;
                RateLimit.TimeWindowInMilliseconds = 60000;
                break;

            case RequestLimit.High:
                RateLimit.MaxAllowedRequests = 500;
                RateLimit.LockoutDurationSeconds = 150;
                RateLimit.TimeWindowInMilliseconds = 120000;
                break;

            case RequestLimit.Unlimited:
                RateLimit.MaxAllowedRequests = 1000;
                RateLimit.LockoutDurationSeconds = 60;
                RateLimit.TimeWindowInMilliseconds = 300000;
                break;
        }
    }

    private void ApplyBandwidthLimit()
    {
        switch (BandwidthLimit)
        {
            case BandwidthLimit.Low:
                Bandwidth.MaxUploadBytesPerSecond = 512 * 1024; // 512KB/s
                Bandwidth.MaxDownloadBytesPerSecond = 512 * 1024;
                Bandwidth.UploadBurstSize = 1024 * 1024; // 1MB burst
                Bandwidth.DownloadBurstSize = 1024 * 1024;
                break;

            case BandwidthLimit.Medium:
                Bandwidth.MaxUploadBytesPerSecond = 1024 * 1024; // 1MB/s
                Bandwidth.MaxDownloadBytesPerSecond = 1024 * 1024;
                Bandwidth.UploadBurstSize = 2 * 1024 * 1024; // 2MB burst
                Bandwidth.DownloadBurstSize = 2 * 1024 * 1024;
                break;

            case BandwidthLimit.High:
                Bandwidth.MaxUploadBytesPerSecond = 5 * 1024 * 1024; // 5MB/s
                Bandwidth.MaxDownloadBytesPerSecond = 5 * 1024 * 1024;
                Bandwidth.UploadBurstSize = 10 * 1024 * 1024; // 10MB burst
                Bandwidth.DownloadBurstSize = 10 * 1024 * 1024;
                break;

            case BandwidthLimit.Unlimited:
                Bandwidth.MaxUploadBytesPerSecond = long.MaxValue;
                Bandwidth.MaxDownloadBytesPerSecond = long.MaxValue;
                Bandwidth.UploadBurstSize = int.MaxValue;
                Bandwidth.DownloadBurstSize = int.MaxValue;
                break;
        }
    }

    private void ApplyConnectionLimit()
    {
        switch (ConnectionLimit)
        {
            case ConnectionLimit.Low:
                Connection.MaxConnectionsPerIpAddress = 20;
                break;

            case ConnectionLimit.Medium:
                Connection.MaxConnectionsPerIpAddress = 100;
                break;

            case ConnectionLimit.High:
                Connection.MaxConnectionsPerIpAddress = 500;
                break;

            case ConnectionLimit.Unlimited:
                Connection.MaxConnectionsPerIpAddress = 1000;
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