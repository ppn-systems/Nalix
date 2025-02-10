using Notio.Network.Firewall.Configuration;
using Notio.Network.Firewall.Enums;
using Notio.Shared.Configuration;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace Notio.Network;

/// <summary>
/// Represents the firewall configuration settings including rate limiting, connection limits, and bandwidth limits.
/// </summary>
public sealed class FirewallConfig : ConfiguredBinder
{
    /// <summary>
    /// Gets or sets a value indicating whether logging is enabled.
    /// </summary>
    /// <value><c>true</c> if logging is enabled; otherwise, <c>false</c>.</value>
    public bool EnableLogging { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether metrics collection is enabled.
    /// </summary>
    /// <value><c>true</c> if metrics collection is enabled; otherwise, <c>false</c>.</value>
    public bool EnableMetrics { get; set; } = true;

    /// <summary>
    /// Gets or sets the bandwidth configuration.
    /// </summary>
    /// <value>The bandwidth configuration.</value>
    [ConfiguredIgnore]
    public BandwidthConfig Bandwidth { get; set; } = new BandwidthConfig();

    /// <summary>
    /// Gets or sets the connection configuration.
    /// </summary>
    /// <value>The connection configuration.</value>
    [ConfiguredIgnore]
    public ConnectionConfig Connection { get; set; } = new ConnectionConfig();

    /// <summary>
    /// Gets or sets the rate limit configuration.
    /// </summary>
    /// <value>The rate limit configuration.</value>
    [ConfiguredIgnore]
    public RateLimitConfig RateLimit { get; set; } = new RateLimitConfig();

    /// <summary>
    /// Gets or sets the bandwidth limit level.
    /// </summary>
    /// <value>The bandwidth limit level.</value>
    [ConfiguredIgnore]
    public BandwidthLimit BandwidthLimit { get; set; } = BandwidthLimit.Medium;

    /// <summary>
    /// Gets or sets the connection limit level.
    /// </summary>
    /// <value>The connection limit level.</value>
    [ConfiguredIgnore]
    public ConnectionLimit ConnectionLimit { get; set; } = ConnectionLimit.Medium;

    /// <summary>
    /// Gets or sets the request limit level.
    /// </summary>
    /// <value>The request limit level.</value>
    [ConfiguredIgnore]
    public RequestLimit RequestLimit { get; set; } = RequestLimit.Medium;

    /// <summary>
    /// Applies the configuration limits for request, bandwidth, and connection.
    /// </summary>
    public void ApplyLimits()
    {
        this.ApplyRequestLimit();
        this.ApplyBandwidthLimit();
        this.ApplyConnectionLimit();
    }

    /// <summary>
    /// Applies the request limit configuration.
    /// </summary>
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

    /// <summary>
    /// Applies the bandwidth limit configuration.
    /// </summary>
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

    /// <summary>
    /// Applies the connection limit configuration.
    /// </summary>
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

    /// <summary>
    /// Validates the firewall configuration.
    /// </summary>
    /// <param name="error">Returns the error message if validation fails.</param>
    /// <returns><c>true</c> if validation is successful; otherwise, <c>false</c>.</returns>
    [RequiresUnreferencedCode("Validation might not work correctly when trimming application code.")]
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
