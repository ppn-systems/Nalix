// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Environment;
using Nalix.Common.Shared;
using Nalix.Framework.Configuration.Binding;
using Nalix.Logging.Exceptions;

namespace Nalix.Logging.Configuration;

/// <summary>
/// Configuration options for the file logger.
/// </summary>
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[System.Diagnostics.DebuggerDisplay("File={LogFileName,nq}, Dir={LogDirectory,nq}, MaxSize={MaxFileSizeBytes}")]
[IniComment("File logger configuration — controls file size, queue, flush behavior, and naming")]
public sealed class FileLogOptions : ConfigurationLoader
{
    #region Constants

    private const System.Int32 DefaultMaxFileSize = 10 * 1024 * 1024; // 10 MB
    private const System.Int32 DefaultMaxQueueSize = 4096;

    #endregion Constants

    #region Fields

    private static readonly System.TimeSpan DefaultFlushInterval = System.TimeSpan.FromSeconds(1);

    private System.Int32 _maxFileSize = DefaultMaxFileSize;
    private System.Int32 _maxQueueSize = DefaultMaxQueueSize;
    private System.String _logFileName = $"log_{System.Environment.MachineName}_.log";

    #endregion Fields

    #region Properties

    /// <summary>
    /// The maximum allowed file size for a log file in bytes.
    /// When this size is reached, a new log file will be created.
    /// </summary>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown when value is less than 1KB or greater than 2GB.</exception>
    [IniComment("Max log file size in bytes before rotation (min 1024, max 33554432)")]
    public System.Int32 MaxFileSizeBytes
    {
        get => _maxFileSize;
        set
        {
            const System.Int32 min = 1024;
            const System.Int32 max = 32 * 1024 * 1024;

            if (value is < min or > max)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(value), $"Value must be between {min} and {max} bytes");
            }

            _maxFileSize = value;
        }
    }

    /// <summary>
    /// The maximum number of entries that can be queued before blocking or discarding.
    /// </summary>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown when value is less than 1.</exception>
    [IniComment("Maximum log entries in the write queue (minimum 1)")]
    public System.Int32 MaxQueueSize
    {
        get => _maxQueueSize;
        set
        {
            if (value < 1)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(value), "Queue size must be at least 1");
            }

            _maxQueueSize = value;
        }
    }

    /// <summary>
    /// Gets or sets the name template for log files.
    /// </summary>
    /// <remarks>
    /// The actual filename may have additional information appended like date or sequence number.
    /// </remarks>
    [IniComment("Log file name template (date and index are appended automatically)")]
    public System.String LogFileName
    {
        get => _logFileName;
        set => _logFileName = System.String.IsNullOrWhiteSpace(value)
            ? $"log_{System.DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}_.log"
            : value;
    }

    /// <summary>
    /// Gets or sets the interval at which log entries are flushed to disk.
    /// </summary>
    /// <remarks>
    /// A shorter interval reduces the risk of data loss but may impact performance.
    /// </remarks>
    [IniComment("How often buffered log entries are written to disk (e.g. 00:00:01 = 1 second)")]
    public System.TimeSpan FlushInterval { get; set; } = DefaultFlushInterval;

    /// <summary>
    /// Gets or sets the behavior when the queue is full.
    /// </summary>
    /// <remarks>
    /// When true, logging will block until queue space is available.
    /// When false, log entries will be discarded when the queue is full.
    /// </remarks>
    [IniComment("Block the caller when the queue is full (false = discard entries instead)")]
    public System.Boolean BlockWhenQueueFull { get; set; } = false;

    /// <summary>
    /// Optional: also suffix by process to avoid cross-process collisions.
    /// </summary>
    [IniComment("Append process name and ID to the file name to avoid multi-process collisions")]
    public System.Boolean UsePerProcessSuffix { get; set; } = false;

    /// <summary>
    /// A custom formatter for the log file name.
    /// </summary>
    [ConfiguredIgnore]
    public System.Func<System.String, System.String>? FormatLogFileName { get; set; }

    /// <summary>
    /// A custom handler for file errors.
    /// </summary>
    [ConfiguredIgnore]
    public System.Action<FileError>? HandleFileError { get; set; }

    #endregion Properties

    #region Methods

    /// <summary>
    /// Gets full path to the current log file.
    /// </summary>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.String GetFullLogFilePath()
        => System.IO.Path.Combine(Directories.LogsDirectory, LogFileName);

    /// <summary>
    /// Builds the exact log file name for a given day and index.
    /// </summary>
    /// <param name="date">The date to include in the file name.</param>
    /// <param name="index">The sequence index for the file on the given date.</param>
    /// <returns>The constructed log file name.</returns>
    public System.String BuildCustomFileName(System.DateTime date, System.Int32 index)
    {
        System.String baseName = LogFileName;

        if (FormatLogFileName != null)
        {
            baseName = FormatLogFileName(baseName);
        }

        System.String ext = System.IO.Path.GetExtension(baseName);
        System.String stem = System.IO.Path.GetFileNameWithoutExtension(baseName);
        System.String datePart = date.ToString("yy_MM_dd");
        System.String newName = $"{stem}_{datePart}_{index}{ext}";

        if (UsePerProcessSuffix)
        {
            using System.Diagnostics.Process p = System.Diagnostics.Process.GetCurrentProcess();
            System.String processSuffix = $"_{p.ProcessName}_{p.Id}";
            System.String stemWithProcess = System.IO.Path.GetFileNameWithoutExtension(newName) + processSuffix;
            newName = stemWithProcess + ext;
        }

        return newName;
    }

    #endregion Methods
}