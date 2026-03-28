// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using Nalix.Common.Abstractions;
using Nalix.Common.Diagnostics;
using Nalix.Framework.Configuration.Binding;

namespace Nalix.Logging.Configuration;

/// <summary>
/// Provides configuration options for the logging system with a fluent interface.
/// </summary>
[ExcludeFromCodeCoverage]
[DebuggerDisplay("Min={MinLevel}, Utc={UseUtcTimestamp}")]
[IniComment("Logging system configuration — controls log level, timestamp format, and entry metadata")]
public sealed class NLogixOptions : ConfigurationLoader, IDisposable
{
    #region Fields

    private int _disposed;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets or sets the minimum logging level. Messages below this level will be ignored.
    /// </summary>
    [IniComment("Minimum log level to process (e.g. Meta, Trace, Debug, Info, Warn, Error, Critical)")]
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
    [IniComment("Timestamp format applied to every log entry (standard .NET date format string)")]
    public string TimestampFormat { get; set; }

    /// <summary>
    /// Gets or sets whether to use UTC time for timestamps.
    /// </summary>
    [IniComment("Use UTC time for timestamps (false = local time)")]
    public bool UseUtcTimestamp { get; set; }

    /// <summary>
    /// Gets or sets whether to include process ID in log entries.
    /// </summary>
    [IniComment("Include the current process ID in each log entry")]
    public bool IncludeProcessId { get; set; }

    /// <summary>
    /// Gets or sets whether to include timestamp in log entries.
    /// </summary>
    [IniComment("Include a timestamp in each log entry")]
    public bool IncludeTimestamp { get; set; }

    /// <summary>
    /// Gets or sets whether to include machine name in log entries.
    /// </summary>
    [IniComment("Include the machine name in each log entry")]
    public bool IncludeMachineName { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of concurrent log processing tasks per target.
    /// </summary>
    [IniComment("Max concurrent log processing tasks per target (increase for high-throughput scenarios)")]
    public int GroupConcurrencyLimit { get; set; }

    #endregion Properties

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="NLogixOptions"/> class.
    /// </summary>
    public NLogixOptions()
    {
        this.Publisher = null;
        this.MinLevel = LogLevel.Info;
        this.FileOptions = new FileLogOptions();

        this.UseUtcTimestamp = true;
        this.IncludeProcessId = true;
        this.IncludeTimestamp = true;
        this.IncludeMachineName = true;
        this.GroupConcurrencyLimit = 3;
        this.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff";
    }

    #endregion Constructors

    #region APIs

    /// <summary>
    /// Sets the log distributor for publishing log messages.
    /// </summary>
    public NLogixOptions SetPublisher(ILogDistributor publisher)
    {
        ArgumentNullException.ThrowIfNull(publisher);
        ObjectDisposedException.ThrowIf(Interlocked
                                      .CompareExchange(ref _disposed, 0, 0) != 0, nameof(NLogixOptions));
        this.Publisher = publisher;
        return this;
    }

    /// <summary>
    /// Applies default configuration settings to the logging configuration.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public NLogixOptions ConfigureDefaults(Func<NLogixOptions, NLogixOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        return configure(this);
    }

    /// <summary>
    /// Sets the configuration options for file logging.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public NLogixOptions ConfigureFileOptions(Action<FileLogOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        ObjectDisposedException.ThrowIf(Interlocked
                                      .CompareExchange(ref _disposed, 0, 0) != 0, nameof(NLogixOptions));
        configure(this.FileOptions);
        return this;
    }

    /// <summary>
    /// Adds a logging target to receive log entries.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public NLogixOptions RegisterTarget(ILoggerTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        ObjectDisposedException.ThrowIf(Interlocked
                                      .CompareExchange(ref _disposed, 0, 0) != 0, nameof(NLogixOptions));
        _ = this.Publisher?.RegisterTarget(target);
        return this;
    }

    /// <summary>
    /// Sets the minimum logging level for filtering log entries.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public NLogixOptions SetMinimumLevel(LogLevel level)
    {
        ObjectDisposedException.ThrowIf(Interlocked
                                      .CompareExchange(ref _disposed, 0, 0) != 0, nameof(NLogixOptions));
        this.MinLevel = level;
        return this;
    }

    /// <summary>
    /// Releases resources used by this instance.
    /// </summary>
    [SuppressMessage("Design", "CA1063:Implement IDisposable Correctly", Justification = "Pattern is intentional")]
    [SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            this.Publisher?.Dispose();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ERROR disposing NLogixOptions: {ex.Message}");
        }

        GC.SuppressFinalize(this);
    }

    #endregion APIs
}
