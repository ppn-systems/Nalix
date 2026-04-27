// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using Nalix.Common.Abstractions;
using Nalix.Environment.Configuration.Binding;
using Nalix.Environment.IO;
using Nalix.Logging.Exceptions;

namespace Nalix.Logging.Options;

/// <summary>Configuration options for file-based logging.</summary>
[ExcludeFromCodeCoverage]
[DebuggerDisplay("File={LogFileName,nq}, Dir={LogDirectory,nq}, MaxSize={MaxFileSizeBytes}")]
[IniComment("File logger configuration — controls file size, queue, flush behavior, and naming")]
public sealed class FileLogOptions : ConfigurationLoader
{
    #region Constants

    /// <summary>The default maximum log file size, in bytes.</summary>
    private const int DefaultMaxFileSize = 10 * 1024 * 1024;
    private const int DefaultMaxQueueSize = 4096;

    #endregion Constants

    #region Properties

    /// <summary>
    /// Gets or sets the maximum allowed size of a log file in bytes.
    /// </summary>
    [IniComment("Max log file size in bytes before rotation (min 1024, max 1GB)")]
    [System.ComponentModel.DataAnnotations.Range(1024, 1024 * 1024 * 1024, ErrorMessage = "MaxFileSizeBytes must be between 1KB and 1GB.")]
    public int MaxFileSizeBytes { get; set; } = DefaultMaxFileSize;

    /// <summary>
    /// Gets or sets the maximum number of queued log entries before blocking or dropping.
    /// </summary>
    [IniComment("Maximum log entries in the write queue (minimum 1)")]
    [System.ComponentModel.DataAnnotations.Range(1, 1000000, ErrorMessage = "MaxQueueSize must be between 1 and 1,000,000.")]
    public int MaxQueueSize { get; set; } = DefaultMaxQueueSize;

    /// <summary>
    /// Gets or sets the base log file name.
    /// </summary>
    [IniComment("Log file name template (date and index are appended automatically)")]
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "LogFileName is required.")]
    public string LogFileName
    {
        get;
        set => field = string.IsNullOrWhiteSpace(value)
            ? $"log_{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}_.log"
            : value;
    } = $"log_{System.Environment.MachineName}_.log";

    /// <summary>
    /// Gets or sets the interval at which buffered log entries are flushed to disk.
    /// </summary>
    [IniComment("How often buffered log entries are written to disk (e.g. 00:00:01 = 1 second)")]
    public TimeSpan FlushInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets whether writers should block when the queue is full.
    /// </summary>
    [IniComment("Block the caller when the queue is full (false = discard entries instead)")]
    public bool BlockWhenQueueFull { get; set; }

    /// <summary>
    /// Gets or sets whether to append the current process name and ID to the file name.
    /// </summary>
    [IniComment("Append process name and ID to the file name to avoid multi-process collisions")]
    public bool UsePerProcessSuffix { get; set; }

    /// <summary>Gets or sets a custom formatter for log file names.</summary>
    [ConfiguredIgnore]
    public Func<string, string>? FormatLogFileName { get; set; }

    /// <summary>Gets or sets the callback used to handle file-related errors.</summary>
    [ConfiguredIgnore]
    public Action<FileError>? HandleFileError { get; set; }

    /// <summary>
    /// Validates the configuration options.
    /// </summary>
    public void Validate()
    {
        System.ComponentModel.DataAnnotations.ValidationContext context = new(this);
        System.ComponentModel.DataAnnotations.Validator.ValidateObject(this, context, validateAllProperties: true);
    }

    #endregion Properties

    #region Methods

    /// <summary>Gets the full path to the current log file.</summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public string GetFullLogFilePath()
        => Path.Combine(Directories.LogsDirectory, this.LogFileName);

    /// <summary>Builds the log file name for the specified date and index.</summary>
    /// <param name="date">The date to include in the file name.</param>
    /// <param name="index">The sequence index for the file on the given date.</param>
    /// <returns>The constructed log file name.</returns>
    public string BuildCustomFileName(DateTime date, int index)
    {
        string baseName = this.LogFileName;

        if (this.FormatLogFileName != null)
        {
            baseName = this.FormatLogFileName(baseName);
        }

        string ext = Path.GetExtension(baseName);
        string stem = Path.GetFileNameWithoutExtension(baseName);
        string datePart = date.ToString("yy_MM_dd", CultureInfo.InvariantCulture);
        string newName = $"{stem}_{datePart}_{index}{ext}";

        if (this.UsePerProcessSuffix)
        {
            using Process p = Process.GetCurrentProcess();
            string processSuffix = $"_{p.ProcessName}_{p.Id}";
            string stemWithProcess = Path.GetFileNameWithoutExtension(newName) + processSuffix;
            newName = stemWithProcess + ext;
        }

        return newName;
    }

    #endregion Methods
}
