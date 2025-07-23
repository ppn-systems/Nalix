using System;

namespace Nalix.Network.Throttling.Settings;

/// <summary>
/// Represents settings for rate limiting, including the Number of requests, lockout duration, and time window.
/// </summary>
public readonly struct RequestLimitSettings
{
    #region Properties

    /// <summary>
    /// Gets the maximum Number of requests allowed.
    /// </summary>
    public Int32 Requests { get; }

    /// <summary>
    /// Gets the duration in seconds to lock out after exceeding the request limit.
    /// </summary>
    public Int32 LockoutDurationSec { get; }

    /// <summary>
    /// Gets the time window in milliseconds for measuring the request rate.
    /// </summary>
    public Int32 TimeWindowMs { get; }

    #endregion Properties

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestLimitSettings"/> struct.
    /// </summary>
    /// <param name="requests">The maximum Number of requests allowed.</param>
    /// <param name="lockoutSeconds">The duration in seconds to lock out after exceeding the request limit.</param>
    /// <param name="windowMilliseconds">The time window in milliseconds for measuring the request rate.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if any parameter is out of valid range.
    /// </exception>
    public RequestLimitSettings(Int32 requests, Int32 lockoutSeconds, Int32 windowMilliseconds)
    {
        if (requests < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requests), "Requests must be at least 1.");
        }

        if (lockoutSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lockoutSeconds), "Lockout duration cannot be negative.");
        }

        if (windowMilliseconds < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(windowMilliseconds), "Time window must be at least 1 ms.");
        }

        this.Requests = requests;
        this.LockoutDurationSec = lockoutSeconds;
        this.TimeWindowMs = windowMilliseconds;
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    /// Returns a string representation of the settings.
    /// </summary>
    public override String ToString() =>
        $"Requests: {this.Requests}, Lockout: {this.LockoutDurationSec}s, Window: {this.TimeWindowMs}ms";

    #endregion Methods
}
