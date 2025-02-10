namespace Notio.Network.Firewall.Enums;

/// <summary>
/// Represents different levels of request limits for a firewall configuration.
/// </summary>
public enum RequestLimit
{
    /// <summary>
    /// Represents a low request limit (few requests per minute).
    /// </summary>
    Low,

    /// <summary>
    /// Represents a medium request limit (average number of requests per minute).
    /// </summary>
    Medium,

    /// <summary>
    /// Represents a high request limit (many requests per minute).
    /// </summary>
    High,

    /// <summary>
    /// Represents no limit on the number of requests.
    /// </summary>
    Unlimited
}
