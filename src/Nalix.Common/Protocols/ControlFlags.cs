namespace Nalix.Common.Protocols;

/// <summary>
/// Control flags providing additional context for control messages.
/// </summary>
[System.Flags]
public enum ControlFlags : System.Byte
{
    /// <summary>
    /// No special flags set.
    /// </summary>
    NONE = 0,

    /// <summary>
    /// Indicates the condition is transient (safe to retry/backoff).
    /// </summary>
    IS_TRANSIENT = 1 << 0,

    /// <summary>
    /// Indicates the error is related to authentication/authorization.
    /// </summary>
    IS_AUTH_RELATED = 1 << 1,

    /// <summary>
    /// Indicates redirect fields are present in Arg0/Arg2.
    /// </summary>
    HAS_REDIRECT = 1 << 2,

    /// <summary>
    /// Indicates the client should reduce its sending rate.
    /// </summary>
    SLOW_DOWN = 1 << 3
}
