namespace Nalix.Common.Logging;

/// <summary>
/// Defines the interface for a logging publisher.
/// </summary>
/// <remarks>
/// A logging publisher is responsible for managing the collection of log targets (e.g., file, console).
/// It provides functionality to add, remove, and publish log entries to different targets.
/// </remarks>
public interface ILogDistributor : System.IDisposable
{
    /// <summary>
    /// Adds a log handler (target) to the publisher, which will be used for publishing log entries.
    /// </summary>
    /// <param name="loggerHandler">The log target to be added.</param>
    /// <returns>The current <see cref="ILogDistributor"/> instance, allowing for method chaining.</returns>
    ILogDistributor AddTarget(ILoggerTarget loggerHandler);

    /// <summary>
    /// Removes a log handler (target) from the publisher.
    /// </summary>
    /// <param name="loggerHandler">The log target to be removed.</param>
    /// <returns><c>true</c> if the target was successfully removed; otherwise, <c>false</c>.</returns>
    System.Boolean RemoveTarget(ILoggerTarget loggerHandler);

    /// <summary>
    /// Publishes the provided log entry to all configured log targets.
    /// </summary>
    /// <param name="entry">The log entry to be published.</param>
    void Publish(LogEntry? entry);
}
