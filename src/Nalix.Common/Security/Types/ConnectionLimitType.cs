namespace Nalix.Common.Security.Enums;

/// <summary>
/// Represents different levels of connection limits that can be applied for managing simultaneous connections.
/// </summary>
public enum ConnectionLimitType : System.Byte
{
    /// <summary>
    /// Represents a low Number of simultaneous connections, typically for minimal traffic environments.
    /// </summary>
    Low = 1,

    /// <summary>
    /// Represents a medium Number of simultaneous connections, suitable for moderate traffic environments.
    /// </summary>
    Medium = 2,

    /// <summary>
    /// Represents a high Number of simultaneous connections, suitable for high-traffic environments.
    /// </summary>
    High = 3,

    /// <summary>
    /// Represents unlimited simultaneous connections, with no restrictions on the Number of connections.
    /// </summary>
    Unlimited = 4
}