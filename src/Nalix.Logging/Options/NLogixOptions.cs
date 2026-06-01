// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Nalix.Abstractions;
using Nalix.Environment.Configuration.Binding;

namespace Nalix.Logging.Options;

/// <summary>
/// Provides configuration options for the logging system.
/// This is a pure POCO — it holds data only and contains no behavior or service references.
/// </summary>
[ExcludeFromCodeCoverage]
[DebuggerDisplay("Min={MinLevel}")]
[IniComment("Logging system configuration — controls log level, timestamp format, and entry metadata")]
public sealed partial class NLogixOptions : ConfigurationLoader, IValidatableConfiguration
{
    #region Properties

    /// <summary>
    /// Gets or sets the minimum logging level. Messages below this level will be ignored.
    /// </summary>
    [IniComment("Minimum log level to process (e.g. Trace, Debug, Info, Warn, Error, Critical)")]
    public LogLevel MinLevel { get; set; }

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
        this.MinLevel = LogLevel.Information;
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
    /// Validates the configuration options.
    /// </summary>
    public void Validate() => this.ValidateDataAnnotations();

    #endregion APIs
}
