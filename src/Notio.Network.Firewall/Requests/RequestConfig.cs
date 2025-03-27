using Notio.Common.Attributes;
using Notio.Shared.Configuration;
using System;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;

namespace Notio.Network.Firewall.Requests;

/// <summary>
/// Represents the configuration settings for rate limiting in the firewall.
/// This configuration defines the maximum number of requests allowed, the lockout duration after exceeding limits,
/// and the time window for counting requests.
/// </summary>
public sealed class RequestConfig : ConfiguredBinder
{
    // Pre-defined configurations to avoid memory allocations
    private static readonly (int Requests, int LockoutSec, int WindowMs) LowSettings = (50, 600, 30_000);
    private static readonly (int Requests, int LockoutSec, int WindowMs) MediumSettings = (100, 300, 60_000);
    private static readonly (int Requests, int LockoutSec, int WindowMs) HighSettings = (500, 150, 120_000);
    private static readonly (int Requests, int LockoutSec, int WindowMs) UnlimitedSettings = (1000, 60, 300_000);

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestConfig"/> class with a specified request limit.
    /// </summary>
    /// <param name="limit">The predefined request limit to apply.</param>
    public RequestConfig(RequestLimit limit)
    {
        (MaxAllowedRequests, LockoutDurationSeconds, TimeWindowInMilliseconds) = GetSettingsForLimit(limit);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestConfig"/> class with a default request limit of <see cref="RequestLimit.Medium"/>.
    /// </summary>
    public RequestConfig()
        : this(RequestLimit.Medium)
    {
    }

    /// <summary>
    /// Gets or sets a value indicating whether metrics collection is enabled.
    /// <c>true</c> if metrics collection is enabled; otherwise, <c>false</c>.
    /// </summary>
    public bool EnableMetrics { get; set; } = true;

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

    /// <summary>
    /// Gets the time window as a TimeSpan.
    /// </summary>
    [ConfiguredIgnore]
    public TimeSpan TimeWindow => TimeSpan.FromMilliseconds(TimeWindowInMilliseconds);

    /// <summary>
    /// Gets the lockout duration as a TimeSpan.
    /// </summary>
    [ConfiguredIgnore]
    public TimeSpan LockoutDuration => TimeSpan.FromSeconds(LockoutDurationSeconds);

    /// <summary>
    /// Gets predefined settings for a request limit level.
    /// </summary>
    /// <param name="limit">The limit level.</param>
    /// <returns>A tuple with rate limit settings.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (int Requests, int LockoutSec, int WindowMs) GetSettingsForLimit(RequestLimit limit) => limit switch
    {
        RequestLimit.Low => LowSettings,
        RequestLimit.Medium => MediumSettings,
        RequestLimit.High => HighSettings,
        RequestLimit.Unlimited => UnlimitedSettings,
        _ => MediumSettings
    };
}
