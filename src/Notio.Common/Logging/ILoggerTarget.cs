namespace Notio.Common.Logging;

/// <summary>
/// Defines an interface for a log processing target.
/// </summary>
public interface ILoggerTarget
{
    /// <summary>
    /// Sends a log message to the processing target.
    /// </summary>
    /// <param name="logMessage">The log message object.</param>
    void Publish(LogEntry logMessage);
}
