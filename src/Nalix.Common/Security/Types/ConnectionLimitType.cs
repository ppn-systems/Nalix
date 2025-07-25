namespace Nalix.Common.Security.Types;

/// <summary>
/// Represents different levels of connection limits that can be applied for managing simultaneous connections.
/// </summary>
public enum ConnectionLimitType : System.Byte
{
    /// <summary>
    /// Represents a low TransportProtocol of simultaneous connections, typically for minimal traffic environments.
    /// </summary>
    Low = 1,

    /// <summary>
    /// Represents a medium TransportProtocol of simultaneous connections, suitable for moderate traffic environments.
    /// </summary>
    Medium = 2,

    /// <summary>
    /// Represents a high TransportProtocol of simultaneous connections, suitable for high-traffic environments.
    /// </summary>
    High = 3,

    /// <summary>
    /// Represents unlimited simultaneous connections, with no restrictions on the TransportProtocol of connections.
    /// </summary>
    Unlimited = 4
}