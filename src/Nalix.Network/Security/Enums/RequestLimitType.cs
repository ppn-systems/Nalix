namespace Nalix.Network.Security.Enums;

/// <summary>
/// Represents different levels of request limits that can be applied to a firewall configuration.
/// These levels define thresholds for the Number of requests allowed.
/// </summary>
public enum RequestLimitType
{
    /// <summary>
    /// Represents a low request limit, typically allowing only a small Number of requests.
    /// </summary>
    Low,

    /// <summary>
    /// Represents a medium request limit, allowing a moderate Number of requests.
    /// </summary>
    Medium,

    /// <summary>
    /// Represents a high request limit, allowing a large Number of requests.
    /// </summary>
    High,

    /// <summary>
    /// Represents an unlimited request limit, with no restrictions on the Number of requests.
    /// </summary>
    Unlimited
}
