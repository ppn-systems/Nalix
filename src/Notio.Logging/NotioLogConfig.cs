using Notio.Common.Logging;
using System;
using System.IO;

namespace Notio.Logging;

/// <summary>
/// Configures logging settings for the application.
/// </summary>
public sealed class NotioLogConfig
{
    private static readonly string _baseDirectory = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
    private readonly ILoggingPublisher _publisher;

    /// <summary>
    /// The minimum logging level required to log messages.
    /// </summary>
    public LoggingLevel MinimumLevel = LoggingLevel.Trace;

    /// <summary>
    /// Indicates whether the default configuration is being used.
    /// </summary>
    internal bool IsDefaults { get; private set; } = true;

    /// <summary>
    /// Gets the directory path where log files are stored.
    /// </summary>
    public string LogDirectory { get; private set; } = Path.Combine(_baseDirectory, "Logs");

    /// <summary>
    /// Gets the default log file name.
    /// </summary>
    public string LogFileName { get; private set; } = "Logging-Notio";

    /// <summary>
    /// Initializes a new instance of the <see cref="NotioLogConfig"/> class.
    /// </summary>
    /// <param name="publisher">The <see cref="ILoggingPublisher"/> instance for publishing log messages.</param>
    internal NotioLogConfig(ILoggingPublisher publisher) => _publisher = publisher;

    /// <summary>
    /// Applies default configuration settings to the logging configuration.
    /// </summary>
    /// <param name="configure">The default configuration action.</param>
    /// <returns>The current <see cref="NotioLogConfig"/> instance.</returns>
    public NotioLogConfig ConfigureDefaults(Func<NotioLogConfig, NotioLogConfig> configure)
        => configure(this);

    /// <summary>
    /// Adds a logging target to the configuration.
    /// </summary>
    /// <param name="target">The <see cref="ILoggingTarget"/> to be added.</param>
    /// <returns>The current <see cref="NotioLogConfig"/> instance.</returns>
    public NotioLogConfig AddTarget(ILoggingTarget target)
    {
        IsDefaults = false;

        _publisher.AddTarget(target);
        return this;
    }

    /// <summary>
    /// Sets the minimum logging level.
    /// </summary>
    /// <param name="level">The minimum <see cref="LoggingLevel"/>.</param>
    /// <returns>The current <see cref="NotioLogConfig"/> instance.</returns>
    public NotioLogConfig SetMinLevel(LoggingLevel level)
    {
        IsDefaults = false;

        MinimumLevel = level;

        return this;
    }

    /// <summary>
    /// Sets the directory path for storing log files.
    /// </summary>
    /// <param name="directory">The new directory path.</param>
    /// <returns>The current <see cref="NotioLogConfig"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when the directory path is invalid.</exception>
    public NotioLogConfig SetLogDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("Invalid directory.", nameof(directory));

        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        IsDefaults = false;

        LogDirectory = directory;
        return this;
    }

    /// <summary>
    /// Sets the name of the log file.
    /// </summary>
    /// <param name="fileName">The new log file name.</param>
    /// <returns>The current <see cref="NotioLogConfig"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when the file name is invalid.</exception>
    public NotioLogConfig SetLogFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("Invalid file name.", nameof(fileName));

        IsDefaults = false;

        LogFileName = fileName;
        return this;
    }
}
