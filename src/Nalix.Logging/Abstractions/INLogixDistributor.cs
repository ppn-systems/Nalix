// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.Logging;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Defines a contract for distributing log entries to one or more logging targets.
/// </summary>
/// <remarks>
/// An <see cref="INLogixDistributor"/> acts as a central dispatcher that holds a collection
/// of <see cref="INLogixTarget"/> instances (e.g., file writer, console output, network logger)
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
/// distributor.Publish(DateTime.UtcNow, LogLevel.Information, new EventId(1001, "Startup"), "Server started.", null);
/// </code>
/// </example>
public interface INLogixDistributor : IDisposable
{
    /// <summary>
    /// Registers a logging target to receive published log entries.
    /// </summary>
    /// <param name="loggerHandler">
    /// The <see cref="INLogixTarget"/> instance to add to the distribution list.
    /// </param>
    /// <returns>
    /// The current <see cref="INLogixDistributor"/> instance to support method chaining.
    /// </returns>
    INLogixDistributor RegisterTarget(INLogixTarget loggerHandler);

    /// <summary>
    /// Unregisters a previously added logging target.
    /// </summary>
    /// <param name="loggerHandler">
    /// The <see cref="INLogixTarget"/> instance to remove from the distribution list.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the target was successfully removed; otherwise, <see langword="false"/>.
    /// </returns>
    bool UnregisterTarget(INLogixTarget loggerHandler);

    /// <summary>
    /// Publishes the specified log entry to all registered logging targets.
    /// </summary>
    void Publish(DateTime timestampUtc, LogLevel logLevel, EventId eventId, string message, Exception? exception);
}
