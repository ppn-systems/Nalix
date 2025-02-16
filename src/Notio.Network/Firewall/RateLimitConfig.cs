using Notio.Shared.Configuration;
using System;
using System.ComponentModel.DataAnnotations;

namespace Notio.Network.Firewall;

/// <summary>
/// Represents the configuration settings for rate limiting in the firewall.
/// This configuration defines the maximum number of requests allowed, the lockout duration after exceeding limits,
/// and the time window for counting requests.
/// </summary>
public sealed class RateLimitConfig : ConfiguredBinder
{
    /// <summary>
    /// Gets or sets the maximum allowed requests per time window.
    /// </summary>
    /// <value>The maximum number of requests allowed in a given time window.</value>
    /// <remarks>Value must be between 1 and 1000 requests per window.</remarks>
    [Range(1, 1000)]
    public int MaxAllowedRequests { get; set; } = 100;

    /// <summary>
    /// Gets or sets the duration in seconds for which an IP is locked out after exceeding the maximum allowed requests.
    /// </summary>
    /// <value>The lockout duration in seconds after exceeding the request limit.</value>
    /// <remarks>Value must be between 1 and 3600 seconds (1 hour).</remarks>
    [Range(1, 3600)]
    public int LockoutDurationSeconds { get; set; } = 300;

    /// <summary>
    /// Gets or sets the time window in milliseconds during which requests are counted.
    /// </summary>
    /// <value>The time window in milliseconds.</value>
    /// <remarks>Value must be greater than or equal to 1000 milliseconds (1 second).</remarks>
    [Range(1000, int.MaxValue)]
    public int TimeWindowInMilliseconds { get; set; } = 60000; // 1 minute
}

/// <summary>
/// Represents different levels of request limits that can be applied to a firewall configuration.
/// These levels define thresholds for the number of requests allowed.
/// </summary>
public enum RequestLimit
{
    /// <summary>
    /// Represents a low request limit, typically allowing only a small number of requests.
    /// </summary>
    Low,

    /// <summary>
    /// Represents a medium request limit, allowing a moderate number of requests.
    /// </summary>
    Medium,

    /// <summary>
    /// Represents a high request limit, allowing a large number of requests.
    /// </summary>
    High,

    /// <summary>
    /// Represents an unlimited request limit, with no restrictions on the number of requests.
    /// </summary>
    Unlimited
}
