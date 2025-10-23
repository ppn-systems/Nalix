// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Logging;

/// <summary>
/// Defines a contract for a log processing target.
/// </summary>
/// <remarks>
/// Implementations of this interface determine how and where log entries are stored or displayed.
/// Examples include writing logs to a file, sending them to a remote server, or displaying them in the console.
/// </remarks>
public interface ILoggerTarget
{
    /// <summary>
    /// Publishes a log entry to the configured log target.
    /// </summary>
    /// <param name="logMessage">
    /// The <see cref="LogEntry"/> to be processed.
    /// </param>
    void Publish(LogEntry logMessage);
}
