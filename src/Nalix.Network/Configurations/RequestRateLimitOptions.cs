using Nalix.Common.Security.Types;
using Nalix.Network.Throttling.Settings;
using Nalix.Shared.Configuration.Attributes;
using Nalix.Shared.Configuration.Binding;

namespace Nalix.Network.Configurations;

/// <summary>
/// Represents the configuration settings for rate limiting in the firewall.
/// This configuration defines the maximum ProtocolType of requests allowed, the lockout duration after exceeding limits,
/// and the time window for counting requests.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="RequestRateLimitOptions"/> class with the specified rate limit settings.
/// </remarks>
/// <param name="settings">The rate limit settings to apply.</param>
public sealed class RequestRateLimitOptions(RequestLimitSettings settings) : ConfigurationLoader
{
    #region Fields

    // Pre-defined configurations to avoid memory allocations

    private static readonly RequestLimitSettings LowSettings = new(10, 1000, 5000);
    private static readonly RequestLimitSettings MediumSettings = new(20, 1000, 3000);
    private static readonly RequestLimitSettings HighSettings = new(50, 1000, 2000);
    private static readonly RequestLimitSettings LoginSettings = new(3, 5000, 10000);

    private static readonly System.Collections.Generic.Dictionary<RequestLimitType, RequestLimitSettings>
        SettingsMap = new()
        {
            { RequestLimitType.Low, LowSettings },
            { RequestLimitType.Medium, MediumSettings },
            { RequestLimitType.High, HighSettings },
            { RequestLimitType.Login, LoginSettings }
        };

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestRateLimitOptions"/> class with the specified rate limit settings.
    /// </summary>
    /// <param name="requests">The maximum ProtocolType of requests allowed.</param>
    /// <param name="lockoutSeconds">The duration in seconds to lock out after exceeding the request limit.</param>
    /// <param name="windowMilliseconds">The time window in milliseconds for measuring the request rate.</param>
    public RequestRateLimitOptions(System.Int32 requests, System.Int32 lockoutSeconds, System.Int32 windowMilliseconds)
        : this(new RequestLimitSettings(requests, lockoutSeconds, windowMilliseconds))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestRateLimitOptions"/> class with a specified request limit.
    /// </summary>
    /// <param name="limit">The predefined request limit to apply.</param>
    public RequestRateLimitOptions(RequestLimitType limit)
        : this(GetSettingsForLimit(limit))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestRateLimitOptions"/> class with a default request limit of <see cref="RequestLimitType.Medium"/>.
    /// </summary>
    public RequestRateLimitOptions()
        : this(RequestLimitType.Medium)
    {
    }

    #endregion Constructors

    #region Properties

    /// <summary>
    /// Gets or sets the maximum allowed requests per time window.
    /// </summary>
    /// <value>The maximum ProtocolType of requests allowed in a given time window.</value>
    /// <remarks>Value must be between 1 and 1000 requests per window.</remarks>
    public System.Int32 MaxAllowedRequests { get; set; } = settings.Requests;

    /// <summary>
    /// Gets or sets the duration in seconds for which an IP is locked out after exceeding the maximum allowed requests.
    /// </summary>
    /// <value>The lockout duration in seconds after exceeding the request limit.</value>
    /// <remarks>Value must be between 1 and 3600 seconds (1 hour).</remarks>
    public System.Int32 LockoutDurationSeconds { get; set; } = settings.LockoutDurationSec;

    /// <summary>
    /// Gets or sets the time window in milliseconds during which requests are counted.
    /// </summary>
    /// <value>The time window in milliseconds.</value>
    /// <remarks>Value must be greater than or equal to 1000 milliseconds (1 second).</remarks>
    public System.Int32 TimeWindowInMilliseconds { get; set; } = settings.TimeWindowMs;

    /// <summary>
    /// Gets the time window as a TimeSpan.
    /// </summary>
    [ConfiguredIgnore]
    public System.TimeSpan TimeWindow => System.TimeSpan.FromMilliseconds(this.TimeWindowInMilliseconds);

    /// <summary>
    /// Gets the lockout duration as a TimeSpan.
    /// </summary>
    [ConfiguredIgnore]
    public System.TimeSpan LockoutDuration => System.TimeSpan.FromSeconds(this.LockoutDurationSeconds);

    #endregion Properties

    #region Private Methods

    /// <summary>
    /// Gets predefined settings for a request limit level.
    /// </summary>
    /// <param name="limit">The limit level.</param>
    /// <returns>A tuple with rate limit settings.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static RequestLimitSettings GetSettingsForLimit(RequestLimitType limit)
        => SettingsMap.TryGetValue(limit, out var settings) ? settings : MediumSettings;

    #endregion Private Methods
}
