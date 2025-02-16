using Notio.Network.Firewall;
using Notio.Shared.Configuration;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace Notio.Network;

/// <summary>
/// Represents the firewall configuration settings including rate limiting, connection limits, and bandwidth limits.
/// This configuration helps manage and enforce security rules for traffic, connections, and request limits.
/// </summary>
public sealed class FirewallConfig : ConfiguredBinder
{
    /// <summary>
    /// Gets or sets a value indicating whether logging is enabled.
    /// <c>true</c> if logging is enabled; otherwise, <c>false</c>.
    /// </summary>
    public bool EnableLogging { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether metrics collection is enabled.
    /// <c>true</c> if metrics collection is enabled; otherwise, <c>false</c>.
    /// </summary>
    public bool EnableMetrics { get; set; } = true;

    /// <summary>
    /// Gets or sets the connection configuration.
    /// </summary>
    /// <value>The connection configuration, which includes the maximum connections per IP address.</value>
    [ConfiguredIgnore]
    public ConnectionConfig Connection { get; set; } = new ConnectionConfig();

    /// <summary>
    /// Gets or sets the rate limit configuration.
    /// </summary>
    /// <value>The rate limit configuration, which defines the limits for requests per time window.</value>
    [ConfiguredIgnore]
    public RateLimitConfig RateLimit { get; set; } = new RateLimitConfig();

    /// <summary>
    /// Gets or sets the connection limit level.
    /// Specifies the level of connection limits applied to each IP address.
    /// </summary>
    /// <value>The connection limit level.</value>
    [ConfiguredIgnore]
    public ConnectionLimit ConnectionLimit { get; set; } = ConnectionLimit.Medium;

    /// <summary>
    /// Gets or sets the request limit level.
    /// Specifies the level of request limits applied to the system.
    /// </summary>
    /// <value>The request limit level.</value>
    [ConfiguredIgnore]
    public RequestLimit RequestLimit { get; set; } = RequestLimit.Medium;

    /// <summary>
    /// Applies the configuration limits for request, bandwidth, and connection.
    /// This method sets up limits according to the configured levels for requests and connections.
    /// </summary>
    public void ApplyLimits()
    {
        this.ApplyRequestLimit();
        this.ApplyConnectionLimit();
    }

    /// <summary>
    /// Applies the request limit configuration based on the <see cref="RequestLimit"/> setting.
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
    /// Applies the connection limit configuration based on the <see cref="ConnectionLimit"/> setting.
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
    /// Validates the firewall configuration to ensure all settings are within acceptable ranges.
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
