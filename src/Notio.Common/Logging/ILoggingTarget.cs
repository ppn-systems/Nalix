using Notio.Common.Models;

namespace Notio.Common.Logging;

/// <summary>
/// Defines an interface for a log processing target.
/// </summary>
public interface ILoggingTarget
{
    /// <summary>
    /// Sends a log message to the processing target.
    /// </summary>
    /// <param name="logMessage">The log message object.</param>
    void Publish(LoggingEntry logMessage);
}
