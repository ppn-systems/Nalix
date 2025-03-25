namespace Notio.Common.Logging;

/// <summary>
/// Represents the severity levels of a log message.
/// </summary>
public enum LoggingLevel
{
    /// <summary>
    /// Level used to represent messages related to metrics (statistics, measurement data).
    /// </summary>
    Meta,

    /// <summary>
    /// Used to represent trace-level messages.
    /// </summary>
    Trace,

    /// <summary>
    /// Used to represent debug-level messages.
    /// </summary>
    Debug,

    /// <summary>
    /// Used to represent informational messages.
    /// </summary>
    Information,

    /// <summary>
    /// Used to represent warning-level messages.
    /// </summary>
    Warning,

    /// <summary>
    /// Used to represent error-level messages.
    /// </summary>
    Error,

    /// <summary>
    /// Used to represent critical-level messages.
    /// </summary>
    Critical,

    /// <summary>
    /// Represents an unspecified or undefined level.
    /// </summary>
    None
}
