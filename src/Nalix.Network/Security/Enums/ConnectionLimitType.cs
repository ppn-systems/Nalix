namespace Nalix.Network.Security.Enums;

/// <summary>
/// Represents different levels of connection limits that can be applied for managing simultaneous connections.
/// </summary>
public enum ConnectionLimitType
{
    /// <summary>
    /// Represents a low Number of simultaneous connections, typically for minimal traffic environments.
    /// </summary>
    Low,

    /// <summary>
    /// Represents a medium Number of simultaneous connections, suitable for moderate traffic environments.
    /// </summary>
    Medium,

    /// <summary>
    /// Represents a high Number of simultaneous connections, suitable for high-traffic environments.
    /// </summary>
    High,

    /// <summary>
    /// Represents unlimited simultaneous connections, with no restrictions on the Number of connections.
    /// </summary>
    Unlimited
}
