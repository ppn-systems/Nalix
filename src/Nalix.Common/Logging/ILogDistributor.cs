namespace Nalix.Common.Logging;

/// <summary>
/// Defines a contract for distributing log entries to one or more logging targets.
/// </summary>
/// <remarks>
/// An <see cref="ILogDistributor"/> acts as a central dispatcher that holds a collection
/// of <see cref="ILoggerTarget"/> instances (e.g., file writer, console output, network logger)
/// and sends each log entry to all registered targets.
/// Implementations should ensure thread safety for target registration and publishing.
/// </remarks>
/// <example>
/// <code>
/// ILogDistributor distributor = new LogDistributor();
/// distributor
///     .AddTarget(new ConsoleLoggerTarget())
///     .AddTarget(new FileLoggerTarget("log.txt"));
///
/// distributor.Publish(new LogEntry(LogLevel.Info, new EventId(1001, "Startup"), "Server started."));
/// </code>
/// </example>
public interface ILogDistributor : System.IDisposable
{
    /// <summary>
    /// Registers a logging target to receive published log entries.
    /// </summary>
    /// <param name="loggerHandler">
    /// The <see cref="ILoggerTarget"/> instance to add to the distribution list.
    /// </param>
    /// <returns>
    /// The current <see cref="ILogDistributor"/> instance to support method chaining.
    /// </returns>
    ILogDistributor AddTarget(ILoggerTarget loggerHandler);

    /// <summary>
    /// Unregisters a previously added logging target.
    /// </summary>
    /// <param name="loggerHandler">
    /// The <see cref="ILoggerTarget"/> instance to remove from the distribution list.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the target was successfully removed; otherwise, <see langword="false"/>.
    /// </returns>
    System.Boolean RemoveTarget(ILoggerTarget loggerHandler);

    /// <summary>
    /// Publishes the specified log entry to all registered logging targets.
    /// </summary>
    /// <param name="entry">
    /// The <see cref="LogEntry"/> to publish, or <see langword="null"/> to ignore.
    /// </param>
    void Publish(LogEntry? entry);

    /// <summary>
    /// Asynchronously publishes the specified log entry to all registered logging targets.
    /// </summary>
    /// <param name="entry">
    /// The <see cref="LogEntry"/> to publish, or <see langword="null"/> to ignore.
    /// </param>
    /// <returns>
    /// A <see cref="System.Threading.Tasks.ValueTask"/> representing the asynchronous operation.
    /// </returns>
    System.Threading.Tasks.ValueTask PublishAsync(LogEntry? entry);
}
