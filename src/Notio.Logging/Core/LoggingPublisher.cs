using Notio.Common.Logging;
using Notio.Common.Models;
using System;
using System.Collections.Concurrent;

namespace Notio.Logging.Core;

/// <summary>
/// Manages and dispatches log entries to registered logging targets.
/// </summary>
public class LoggingPublisher : ILoggingPublisher
{
    private readonly ConcurrentBag<ILoggingTarget> _targets = new();

    /// <summary>
    /// Publishes a log entry to all registered logging targets.
    /// </summary>
    /// <param name="entry">The log entry to be published.</param>
    public void Publish(LoggingEntry entry)
    {
        foreach (var target in _targets)
        {
            target.Publish(entry);
        }
    }

    /// <summary>
    /// Adds a logging target to receive log entries.
    /// </summary>
    /// <param name="target">The logging target to add.</param>
    /// <returns>The current instance of <see cref="ILoggingPublisher"/>, allowing method chaining.</returns>
    public ILoggingPublisher AddTarget(ILoggingTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        _targets.Add(target);
        return this;
    }

    /// <summary>
    /// Removes a logging target from the publisher.
    /// </summary>
    /// <param name="target">The logging target to remove.</param>
    /// <returns><c>true</c> if the target was successfully removed; otherwise, <c>false</c>.</returns>
    public bool RemoveTarget(ILoggingTarget target) => _targets.TryTake(out _);
}