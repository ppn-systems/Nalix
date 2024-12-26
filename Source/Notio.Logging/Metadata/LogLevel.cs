namespace Notio.Logging.Metadata;

/// <summary>
/// Represents the severity level of a log message.
/// </summary>
public enum LogLevel
{
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
    /// Represents a non-specific or unspecified log level.
    /// </summary>
    None
}