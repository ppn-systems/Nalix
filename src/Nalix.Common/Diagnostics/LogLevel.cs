// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Diagnostics;

/// <summary>
/// Defines the severity level of a log message.
/// </summary>
/// <remarks>
/// The severity level is used to categorize and filter log output.
/// Lower numeric values typically indicate more detailed or verbose logging.
/// </remarks>
public enum LogLevel : System.Byte
{
    /// <summary>
    /// Represents an undefined or disabled logging level.
    /// No log output will be generated for this level.
    /// </summary>
    None = 0,

    /// <summary>
    /// Metric-level logging.
    /// Used for statistical or measurement data (e.g., performance metrics, counters).
    /// </summary>
    Meta = 1,

    /// <summary>
    /// Trace-level logging.
    /// Contains the most detailed messages, typically used for diagnosing specific issues.
    /// </summary>
    Trace = 2,

    /// <summary>
    /// Debug-level logging.
    /// Used for general debugging information, less verbose than trace.
    /// </summary>
    Debug = 3,

    /// <summary>
    /// Informational messages.
    /// Used to track application flow or significant runtime events.
    /// </summary>
    Information = 4,

    /// <summary>
    /// Warning-level logging.
    /// Indicates a potential issue or unexpected situation that does not cause the application to stop.
    /// </summary>
    Warning = 5,

    /// <summary>
    /// ERROR-level logging.
    /// Indicates a failure in the current operation or request that may require attention.
    /// </summary>
    Error = 6,

    /// <summary>
    /// Critical-level logging.
    /// Indicates a serious failure that may cause the application to stop or become unstable.
    /// </summary>
    Critical = 7,
}
