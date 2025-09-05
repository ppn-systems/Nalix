// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Logging.Abstractions;
using Nalix.Common.Logging.Models;
using Nalix.Logging.Sinks.File;

namespace Nalix.Logging.Engine;

/// <summary>
/// Provides configuration options for the logging system with a fluent interface.
/// </summary>
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[System.Diagnostics.DebuggerDisplay("Min={MinLevel}, Utc={UseUtcTimestamp}")]
public sealed class LogOptions : System.IDisposable
{
    #region Fields

    private readonly ILogDistributor _publisher;
    private System.Int32 _disposed;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the file logger configuration options.
    /// </summary>
    public FileLogOptions FileOptions { get; } = new();

    /// <summary>
    /// Gets or sets the minimum logging level. Messages below this level will be ignored.
    /// </summary>
    public LogLevel MinLevel { get; set; } = LogLevel.Meta;

    /// <summary>
    /// Gets or sets whether to include machine name in log entries.
    /// </summary>
    public System.Boolean IncludeMachineName { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to include process ProtocolType in log entries.
    /// </summary>
    public System.Boolean IncludeProcessId { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to include timestamp in log entries.
    /// </summary>
    public System.Boolean IncludeTimestamp { get; set; } = true;

    /// <summary>
    /// Gets or sets the timestamp format for log entries.
    /// </summary>
    public System.String TimestampFormat { get; set; } = "yyyy-MM-dd HH:mm:ss.fff";

    /// <summary>
    /// Gets or sets whether to use UTC time for timestamps.
    /// </summary>
    public System.Boolean UseUtcTimestamp { get; set; } = true;

    #endregion Properties

    /// <summary>
    /// Initializes a new instance of the <see cref="LogOptions"/> class.
    /// </summary>
    /// <param name="publisher">The <see cref="ILogDistributor"/> instance for publishing log messages.</param>
    internal LogOptions(ILogDistributor publisher)
        => _publisher = publisher ?? throw new System.ArgumentNullException(nameof(publisher));

    /// <summary>
    /// Applies default configuration settings to the logging configuration.
    /// </summary>
    /// <param name="configure">The default configuration action.</param>
    /// <returns>The current <see cref="LogOptions"/> instance for method chaining.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public LogOptions ConfigureDefaults(System.Func<LogOptions, LogOptions> configure)
    {
        System.ArgumentNullException.ThrowIfNull(configure);
        return configure(this);
    }

    /// <summary>
    /// Adds a logging target to receive log entries.
    /// </summary>
    /// <param name="target">The <see cref="ILoggerTarget"/> to add.</param>
    /// <returns>The current <see cref="LogOptions"/> instance for method chaining.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown if target is null.</exception>
    /// <exception cref="System.ObjectDisposedException">Thrown if this instance is disposed.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public LogOptions AddTarget(ILoggerTarget target)
    {
        System.ArgumentNullException.ThrowIfNull(target);
        this.ThrowIfDisposed();

        _ = _publisher.AddTarget(target);
        return this;
    }

    /// <summary>
    /// Sets the minimum logging level for filtering log entries.
    /// </summary>
    /// <param name="level">The minimum <see cref="LogLevel"/>.</param>
    /// <returns>The current <see cref="LogOptions"/> instance for method chaining.</returns>
    /// <exception cref="System.ObjectDisposedException">Thrown if this instance is disposed.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public LogOptions SetMinLevel(LogLevel level)
    {
        this.ThrowIfDisposed();

        MinLevel = level;
        return this;
    }

    /// <summary>
    /// Sets the configuration options for file logging.
    /// </summary>
    /// <param name="configure">Action that configures the <see cref="FileLogOptions"/>.</param>
    /// <returns>The current <see cref="LogOptions"/> instance for method chaining.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown if configure is null.</exception>
    /// <exception cref="System.ObjectDisposedException">Thrown if this instance is disposed.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public LogOptions SetFileOptions(System.Action<FileLogOptions> configure)
    {
        System.ArgumentNullException.ThrowIfNull(configure);
        this.ThrowIfDisposed();

        // Apply the configuration to the FileLogOptions instance
        configure(FileOptions);
        return this;
    }

    /// <summary>
    /// Checks whether this instance is disposed and throws an exception if it is.
    /// </summary>
    /// <exception cref="System.ObjectDisposedException">Thrown if this instance is disposed.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private void ThrowIfDisposed()
        => System.ObjectDisposedException.ThrowIf(System.Threading.Interlocked
                                         .CompareExchange(ref _disposed, 0, 0) != 0, nameof(LogOptions));

    /// <summary>
    /// Releases resources used by this instance.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design", "CA1063:Implement IDisposable Correctly",
        Justification = "Pattern is intentional and calls GC.SuppressFinalize")]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
    public void Dispose()
    {
        // Thread-safe disposal check
        if (System.Threading.Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            _publisher.Dispose();
        }
        catch (System.Exception ex)
        {
            // Log any disposal errors to debug output
            System.Diagnostics.Debug.WriteLine($"ERROR disposing LogOptions: {ex.Message}");
        }

        System.GC.SuppressFinalize(this);
    }
}
