using Notio.Common.Logging;
using System;
using System.Threading;

namespace Notio.Logging.Options;

/// <summary>
/// Provides configuration options for the logging system with a fluent interface.
/// </summary>
public sealed class LogOptions : IDisposable
{
    #region Fields

    private readonly ILogDistributor _publisher;
    private int _disposed;

    // Default values that can be customized
    private LogLevel _minLevel = LogLevel.Trace;

    #endregion

    #region Properties

    /// <summary>
    /// Gets the file logger configuration options.
    /// </summary>
    public FileLogOptions FileOptions { get; } = new();

    /// <summary>
    /// Gets or sets the minimum logging level. Messages below this level will be ignored.
    /// </summary>
    public LogLevel MinLevel
    {
        get => _minLevel;
        set => _minLevel = value;
    }

    /// <summary>
    /// Gets or sets whether to include machine name in log entries.
    /// </summary>
    public bool IncludeMachineName { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to include process Number in log entries.
    /// </summary>
    public bool IncludeProcessId { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to include timestamp in log entries.
    /// </summary>
    public bool IncludeTimestamp { get; set; } = true;

    /// <summary>
    /// Gets or sets the timestamp format for log entries.
    /// </summary>
    public string TimestampFormat { get; set; } = "yyyy-MM-dd HH:mm:ss.fff";

    /// <summary>
    /// Gets or sets whether to use UTC time for timestamps.
    /// </summary>
    public bool UseUtcTimestamp { get; set; } = true;

    #endregion

    /// <summary>
    /// Initializes a new instance of the <see cref="LogOptions"/> class.
    /// </summary>
    /// <param name="publisher">The <see cref="ILogDistributor"/> instance for publishing log messages.</param>
    internal LogOptions(ILogDistributor publisher)
        => _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));

    /// <summary>
    /// Applies default configuration settings to the logging configuration.
    /// </summary>
    /// <param name="configure">The default configuration action.</param>
    /// <returns>The current <see cref="LogOptions"/> instance for method chaining.</returns>
    public LogOptions ConfigureDefaults(Func<LogOptions, LogOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        return configure(this);
    }

    /// <summary>
    /// Adds a logging target to receive log entries.
    /// </summary>
    /// <param name="target">The <see cref="ILoggerTarget"/> to add.</param>
    /// <returns>The current <see cref="LogOptions"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if target is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if this instance is disposed.</exception>
    public LogOptions AddTarget(ILoggerTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);

        ThrowIfDisposed();

        _publisher.AddTarget(target);
        return this;
    }

    /// <summary>
    /// Sets the minimum logging level for filtering log entries.
    /// </summary>
    /// <param name="level">The minimum <see cref="LogLevel"/>.</param>
    /// <returns>The current <see cref="LogOptions"/> instance for method chaining.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if this instance is disposed.</exception>
    public LogOptions SetMinLevel(LogLevel level)
    {
        ThrowIfDisposed();

        MinLevel = level;
        return this;
    }

    /// <summary>
    /// Sets the configuration options for file logging.
    /// </summary>
    /// <param name="configure">Action that configures the <see cref="FileLogOptions"/>.</param>
    /// <returns>The current <see cref="LogOptions"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if configure is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if this instance is disposed.</exception>
    public LogOptions SetFileOptions(Action<FileLogOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        ThrowIfDisposed();

        // Apply the configuration to the FileLogOptions instance
        configure(FileOptions);
        return this;
    }

    /// <summary>
    /// Checks whether this instance is disposed and throws an exception if it is.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown if this instance is disposed.</exception>
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(Interlocked.CompareExchange(ref _disposed, 0, 0) != 0, nameof(LogOptions));
    }

    /// <summary>
    /// Releases resources used by this instance.
    /// </summary>
    public void Dispose()
    {
        // Thread-safe disposal check
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        try
        {
            _publisher.Dispose();
        }
        catch (Exception ex)
        {
            // Log any disposal errors to debug output
            System.Diagnostics.Debug.WriteLine($"Error disposing LogOptions: {ex.Message}");
        }

        GC.SuppressFinalize(this);
    }
}
