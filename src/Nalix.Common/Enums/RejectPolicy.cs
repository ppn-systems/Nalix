namespace Nalix.Common.Enums;

/// <summary>
/// Represents policies for handling rejected connections.
/// </summary>
public enum RejectPolicy
{
    /// <summary>
    /// Rejects the new incoming connection.
    /// </summary>
    REJECT_NEW = 0,

    /// <summary>
    /// Drops the oldest anonymous connection to make room for the new one.
    /// </summary>
    DROP_OLDEST_ANONYMOUS = 1
}
