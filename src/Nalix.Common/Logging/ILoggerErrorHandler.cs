namespace Nalix.Common.Logging;

/// <summary>
/// Interface for logging targets that implement custom error handling.
/// </summary>
public interface ILoggerErrorHandler
{
    /// <summary>
    /// Handles errors that occur during log publishing.
    /// </summary>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="entry">The log entry that was being processed.</param>
    void HandleError(System.Exception exception, LogEntry entry);
}
