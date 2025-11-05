namespace Nalix.Common.Enums;

/// <summary>
/// Represents policies for handling rejected connections.
/// </summary>
public enum RejectPolicy
{
    /// <summary>
    /// Rejects the new incoming connection.
    /// </summary>
    RejectNew = 0,

    /// <summary>
    /// Drops the oldest anonymous connection to make room for the new one.
    /// </summary>
    DropOldestAnonymous = 1
}
