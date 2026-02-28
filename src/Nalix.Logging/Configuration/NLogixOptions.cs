// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Attributes;
using Nalix.Common.Diagnostics;
using Nalix.Framework.Configuration.Binding;

namespace Nalix.Logging.Configuration;

/// <summary>
/// Provides configuration options for the logging system with a fluent interface.
/// </summary>
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[System.Diagnostics.DebuggerDisplay("Min={MinLevel}, Utc={UseUtcTimestamp}")]
public sealed class NLogixOptions : ConfigurationLoader, System.IDisposable
{
    #region Fields

    private System.Int32 _disposed = 0;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets or sets the minimum logging level. Messages below this level will be ignored.
    /// </summary>
    public LogLevel MinLevel { get; set; }

    /// <summary>
    /// Gets or sets the log distributor responsible for publishing log messages to targets.
    /// </summary>
    [ConfiguredIgnore]
    private ILogDistributor? Publisher { get; set; }

    /// <summary>
    /// Gets the file logger configuration options.
    /// </summary>
    [ConfiguredIgnore]
    public FileLogOptions FileOptions { get; }

    /// <summary>
    /// Gets or sets the timestamp format for log entries.
    /// </summary>
    public System.String TimestampFormat { get; set; }

    /// <summary>
    /// Gets or sets whether to use UTC time for timestamps.
    /// </summary>
    public System.Boolean UseUtcTimestamp { get; set; }

    /// <summary>
    /// Gets or sets whether to include process ProtocolType in log entries.
    /// </summary>
    public System.Boolean IncludeProcessId { get; set; }

    /// <summary>
    /// Gets or sets whether to include timestamp in log entries.
    /// </summary>
    public System.Boolean IncludeTimestamp { get; set; }

    /// <summary>
    /// Gets or sets whether to include machine name in log entries.
    /// </summary>
    public System.Boolean IncludeMachineName { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of concurrent log processing tasks per target.
    /// </summary>
    public System.Int32 GroupConcurrencyLimit { get; set; }

    #endregion Properties

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="NLogixOptions"/> class.
    /// </summary>
    public NLogixOptions()
    {
        Publisher = null;
        MinLevel = LogLevel.Meta;
        FileOptions = new FileLogOptions();

        UseUtcTimestamp = true;
        IncludeProcessId = true;
        IncludeTimestamp = true;
        IncludeMachineName = true;
        GroupConcurrencyLimit = 3;
        TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff";
    }

    #endregion Constructors

    #region APIs

    /// <summary>
    /// Sets the log distributor for publishing log messages. This method allows you to replace the default log distributor with a custom implementation.
    /// </summary>
    /// <param name="publisher">The <see cref="ILogDistributor"/> instance for publishing log messages.</param>
    /// <returns>The current <see cref="NLogixOptions"/> instance for method chaining.</returns>
    public NLogixOptions SetPublisher(ILogDistributor publisher)
    {
        System.ArgumentNullException.ThrowIfNull(publisher);
        System.ObjectDisposedException.ThrowIf(System.Threading.Interlocked
                                      .CompareExchange(ref _disposed, 0, 0) != 0, nameof(NLogixOptions));
        Publisher = publisher;
        return this;
    }

    /// <summary>
    /// Applies default configuration settings to the logging configuration.
    /// </summary>
    /// <param name="configure">The default configuration action.</param>
    /// <returns>The current <see cref="NLogixOptions"/> instance for method chaining.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public NLogixOptions ConfigureDefaults(System.Func<NLogixOptions, NLogixOptions> configure)
    {
        System.ArgumentNullException.ThrowIfNull(configure);
        return configure(this);
    }

    /// <summary>
    /// Sets the configuration options for file logging.
    /// </summary>
    /// <param name="configure">Action that configures the <see cref="FileLogOptions"/>.</param>
    /// <returns>The current <see cref="NLogixOptions"/> instance for method chaining.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown if configure is null.</exception>
    /// <exception cref="System.ObjectDisposedException">Thrown if this instance is disposed.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public NLogixOptions ConfigureFileOptions(System.Action<FileLogOptions> configure)
    {
        System.ArgumentNullException.ThrowIfNull(configure);
        System.ObjectDisposedException.ThrowIf(System.Threading.Interlocked
                                      .CompareExchange(ref _disposed, 0, 0) != 0, nameof(NLogixOptions));

        // Apply the configuration to the FileLogOptions instance
        configure(FileOptions);
        return this;
    }

    /// <summary>
    /// Adds a logging target to receive log entries.
    /// </summary>
    /// <param name="target">The <see cref="ILoggerTarget"/> to add.</param>
    /// <returns>The current <see cref="NLogixOptions"/> instance for method chaining.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown if target is null.</exception>
    /// <exception cref="System.ObjectDisposedException">Thrown if this instance is disposed.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public NLogixOptions RegisterTarget(ILoggerTarget target)
    {
        System.ArgumentNullException.ThrowIfNull(target);
        System.ObjectDisposedException.ThrowIf(System.Threading.Interlocked
                                      .CompareExchange(ref _disposed, 0, 0) != 0, nameof(NLogixOptions));

        _ = Publisher?.RegisterTarget(target);
        return this;
    }

    /// <summary>
    /// Sets the minimum logging level for filtering log entries.
    /// </summary>
    /// <param name="level">The minimum <see cref="LogLevel"/>.</param>
    /// <returns>The current <see cref="NLogixOptions"/> instance for method chaining.</returns>
    /// <exception cref="System.ObjectDisposedException">Thrown if this instance is disposed.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public NLogixOptions SetMinimumLevel(LogLevel level)
    {
        System.ObjectDisposedException.ThrowIf(System.Threading.Interlocked
                                      .CompareExchange(ref _disposed, 0, 0) != 0, nameof(NLogixOptions));

        MinLevel = level;
        return this;
    }

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
            Publisher?.Dispose();
        }
        catch (System.Exception ex)
        {
            // Log any disposal errors to debug output
            System.Diagnostics.Debug.WriteLine($"ERROR disposing NLogixOptions: {ex.Message}");
        }

        System.GC.SuppressFinalize(this);
    }

    #endregion APIs
}
