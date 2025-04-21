using Notio.Common.Logging;

namespace Notio.Logging;

public sealed partial class NLogix
{
    // Sanitize log message to prevent log forging
    // Removes potentially dangerous characters (e.g., newlines or control characters)
    internal static string SanitizeLogMessage(string? message)
        => message?.Replace("\n", "").Replace("\r", "") ?? string.Empty;

    /// <summary>
    /// Writes a log entry with the specified level, event Number, message, and optional exception.
    /// </summary>
    /// <param name="level">The log level (e.g., Info, Warning, Error, etc.).</param>
    /// <param name="eventId">The event Number to associate with the log entry.</param>
    /// <param name="message">The log message.</param>
    /// <param name="exception">Optional exception associated with the log entry.</param>
    internal void WriteLog(LogLevel level, EventId eventId, string message, System.Exception? exception = null)
       => base.CreateLogEntry(level, eventId, message, exception);
}
