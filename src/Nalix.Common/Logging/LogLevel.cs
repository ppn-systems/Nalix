namespace Nalix.Common.Logging;

/// <summary>
/// Represents the severity levels of a log message.
/// </summary>
public enum LogLevel : System.Byte
{
    /// <summary>
    /// Level used to represent messages related to metrics (statistics, measurement data).
    /// </summary>
    Meta = 1,

    /// <summary>
    /// Used to represent trace-level messages.
    /// </summary>
    Trace = 2,

    /// <summary>
    /// Used to represent debug-level messages.
    /// </summary>
    Debug = 3,

    /// <summary>
    /// Used to represent informational messages.
    /// </summary>
    Information = 4,

    /// <summary>
    /// Used to represent warning-level messages.
    /// </summary>
    Warning = 5,

    /// <summary>
    /// Used to represent error-level messages.
    /// </summary>
    Error = 6,

    /// <summary>
    /// Used to represent critical-level messages.
    /// </summary>
    Critical = 7,

    /// <summary>
    /// Represents an unspecified or undefined level.
    /// </summary>
    None = 255
}
