using Notio.Shared.Configuration;
using System;
using System.ComponentModel.DataAnnotations;

namespace Notio.Network.Firewall.Configuration;

/// <summary>
/// Represents the configuration settings for rate limiting.
/// </summary>
public sealed class RateLimitConfig : ConfiguredBinder
{
    /// <summary>
    /// Gets or sets the maximum allowed requests per time window.
    /// </summary>
    /// <value>The maximum number of requests allowed.</value>
    /// <remarks>Value must be between 1 and 1000.</remarks>
    [Range(1, 1000)]
    public int MaxAllowedRequests { get; set; } = 100;

    /// <summary>
    /// Gets or sets the duration in seconds for which an IP is locked out after exceeding the maximum allowed requests.
    /// </summary>
    /// <value>The lockout duration in seconds.</value>
    /// <remarks>Value must be between 1 and 3600.</remarks>
    [Range(1, 3600)]
    public int LockoutDurationSeconds { get; set; } = 300;

    /// <summary>
    /// Gets or sets the time window in milliseconds during which the requests are counted.
    /// </summary>
    /// <value>The time window in milliseconds.</value>
    /// <remarks>Value must be greater than or equal to 1000 milliseconds (1 second).</remarks>
    [Range(1000, int.MaxValue)]
    public int TimeWindowInMilliseconds { get; set; } = 60000; // 1 minute
}
