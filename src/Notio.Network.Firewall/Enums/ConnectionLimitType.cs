namespace Notio.Network.Security.Enums;

/// <summary>
/// Represents different levels of connection limits that can be applied for managing simultaneous connections.
/// </summary>
public enum ConnectionLimitType
{
    /// <summary>
    /// Represents a low number of simultaneous connections, typically for minimal traffic environments.
    /// </summary>
    Low,

    /// <summary>
    /// Represents a medium number of simultaneous connections, suitable for moderate traffic environments.
    /// </summary>
    Medium,

    /// <summary>
    /// Represents a high number of simultaneous connections, suitable for high-traffic environments.
    /// </summary>
    High,

    /// <summary>
    /// Represents unlimited simultaneous connections, with no restrictions on the number of connections.
    /// </summary>
    Unlimited
}
