namespace Nalix.Common.Security.Types;

/// <summary>
/// Represents different levels of request limits that can be applied to a firewall configuration.
/// These levels define thresholds for the TransportProtocol of requests allowed.
/// </summary>
public enum RequestLimitType : System.Byte
{
    /// <summary>
    /// Represents a low request limit, typically allowing only a small TransportProtocol of requests.
    /// </summary>
    Low = 1,

    /// <summary>
    /// Represents a medium request limit, allowing a moderate TransportProtocol of requests.
    /// </summary>
    Medium = 2,

    /// <summary>
    /// Represents a high request limit, allowing a large TransportProtocol of requests.
    /// </summary>
    High = 3,

    /// <summary>
    /// Represents an unlimited request limit, with no restrictions on the TransportProtocol of requests.
    /// </summary>
    Login = 4
}