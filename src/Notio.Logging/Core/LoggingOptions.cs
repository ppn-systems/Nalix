using Notio.Common.Cryptography;
using Notio.Common.Logging;
using Notio.Logging.Internal.File;
using System;

namespace Notio.Logging.Core;

/// <summary>
/// Configures logging settings for the application.
/// </summary>
public sealed class LoggingOptions : IDisposable
{
    private readonly ILoggingPublisher _publisher;
    private bool _disposed;

    internal FileLoggerOptions FileOptions { get; init; } = new();

    /// <summary>
    /// The minimum logging level for the file logger.
    /// </summary>
    public LoggingLevel MinLevel { get; private set; } = LoggingLevel.Trace;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggingOptions"/> class.
    /// </summary>
    /// <param name="publisher">The <see cref="ILoggingPublisher"/> instance for publishing log messages.</param>
    internal LoggingOptions(ILoggingPublisher publisher) => _publisher = publisher;

    /// <summary>
    /// Applies default configuration settings to the logging configuration.
    /// </summary>
    /// <param name="configure">The default configuration action.</param>
    /// <returns>The current <see cref="LoggingOptions"/> instance.</returns>
    public LoggingOptions ConfigureDefaults(Func<LoggingOptions, LoggingOptions> configure)
        => configure(this);

    /// <summary>
    /// Adds a logging target to the configuration.
    /// </summary>
    /// <param name="target">The <see cref="ILoggingTarget"/> to be added.</param>
    /// <returns>The current <see cref="LoggingOptions"/> instance.</returns>
    public LoggingOptions AddTarget(ILoggingTarget target)
    {
        _publisher.AddTarget(target);
        return this;
    }

    /// <summary>
    /// Sets the minimum logging level.
    /// </summary>
    /// <param name="level">The minimum <see cref="LoggingLevel"/>.</param>
    /// <returns>The current <see cref="LoggingOptions"/> instance.</returns>
    public LoggingOptions SetMinLevel(LoggingLevel level)
    {
        MinLevel = level;
        return this;
    }

    /// <summary>
    /// Sets the configuration options for file logging.
    /// </summary>
    /// <param name="configure">
    /// An action that allows configuration of the <see cref="FileLoggerOptions"/>.
    /// </param>
    /// <returns>
    /// The current <see cref="LoggingOptions"/> instance, allowing for method chaining.
    /// </returns>
    public LoggingOptions SetFileOptions(Action<FileLoggerOptions> configure)
    {
        // Apply the configuration to the FileLoggerOptions instance
        configure(FileOptions);
        return this;
    }

    /// <summary>
    /// Releases unmanaged resources and performs other cleanup operations.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases unmanaged and - optionally - managed resources.
    /// </summary>
    /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
    private void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _publisher.Dispose();
            }

            _disposed = true;
        }
    }

    /// <summary>
    /// Finalizer for the <see cref="LoggingOptions"/> class.
    /// </summary>
    ~LoggingOptions()
    {
        Dispose(false);
    }
}
