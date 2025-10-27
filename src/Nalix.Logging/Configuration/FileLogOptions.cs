// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using Nalix.Common.Environment;
using Nalix.Common.Shared;
using Nalix.Framework.Configuration.Binding;
using Nalix.Logging.Exceptions;

namespace Nalix.Logging.Configuration;

/// <summary>
/// Configuration options for the file logger.
/// </summary>
[ExcludeFromCodeCoverage]
[DebuggerDisplay("File={LogFileName,nq}, Dir={LogDirectory,nq}, MaxSize={MaxFileSizeBytes}")]
[IniComment("File logger configuration — controls file size, queue, flush behavior, and naming")]
public sealed class FileLogOptions : ConfigurationLoader
{
    #region Constants

    /// <summary>
    /// 10 MB
    /// </summary>
    private const int DefaultMaxFileSize = 10 * 1024 * 1024;
    private const int DefaultMaxQueueSize = 4096;

    #endregion Constants

    #region Fields

    private static readonly TimeSpan DefaultFlushInterval = TimeSpan.FromSeconds(1);

    #endregion Fields

    #region Properties

    /// <summary>
    /// The maximum allowed file size for a log file in bytes.
    /// When this size is reached, a new log file will be created.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when value is less than 1KB or greater than 2GB.</exception>
    [IniComment("Max log file size in bytes before rotation (min 1024, max 33554432)")]
    public int MaxFileSizeBytes
    {
        get;
        set
        {
            const int min = 1024;
            const int max = 32 * 1024 * 1024;

            if (value is < min or > max)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value), $"Value must be between {min} and {max} bytes");
            }

            field = value;
        }
    } = DefaultMaxFileSize;

    /// <summary>
    /// The maximum number of entries that can be queued before blocking or discarding.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when value is less than 1.</exception>
    [IniComment("Maximum log entries in the write queue (minimum 1)")]
    public int MaxQueueSize
    {
        get;
        set
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value), "Queue size must be at least 1");
            }

            field = value;
        }
    } = DefaultMaxQueueSize;

    /// <summary>
    /// Gets or sets the name template for log files.
    /// </summary>
    /// <remarks>
    /// The actual filename may have additional information appended like date or sequence number.
    /// </remarks>
    [IniComment("Log file name template (date and index are appended automatically)")]
    public string LogFileName
    {
        get;
        set => field = string.IsNullOrWhiteSpace(value)
            ? $"log_{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}_.log"
            : value;
    } = $"log_{Environment.MachineName}_.log";

    /// <summary>
    /// Gets or sets the interval at which log entries are flushed to disk.
    /// </summary>
    /// <remarks>
    /// A shorter interval reduces the risk of data loss but may impact performance.
    /// </remarks>
    [IniComment("How often buffered log entries are written to disk (e.g. 00:00:01 = 1 second)")]
    public TimeSpan FlushInterval { get; set; } = DefaultFlushInterval;

    /// <summary>
    /// Gets or sets the behavior when the queue is full.
    /// </summary>
    /// <remarks>
    /// When true, logging will block until queue space is available.
    /// When false, log entries will be discarded when the queue is full.
    /// </remarks>
    [IniComment("Block the caller when the queue is full (false = discard entries instead)")]
    public bool BlockWhenQueueFull { get; set; }

    /// <summary>
    /// Optional: also suffix by process to avoid cross-process collisions.
    /// </summary>
    [IniComment("Append process name and ID to the file name to avoid multi-process collisions")]
    public bool UsePerProcessSuffix { get; set; }

    /// <summary>
    /// A custom formatter for the log file name.
    /// </summary>
    [ConfiguredIgnore]
    public Func<string, string>? FormatLogFileName { get; set; }

    /// <summary>
    /// A custom handler for file errors.
    /// </summary>
    [ConfiguredIgnore]
    public Action<FileError>? HandleFileError { get; set; }

    #endregion Properties

    #region Methods

    /// <summary>
    /// Gets full path to the current log file.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining |
        MethodImplOptions.AggressiveOptimization)]
    public string GetFullLogFilePath()
        => Path.Combine(Directories.LogsDirectory, LogFileName);

    /// <summary>
    /// Builds the exact log file name for a given day and index.
    /// </summary>
    /// <param name="date">The date to include in the file name.</param>
    /// <param name="index">The sequence index for the file on the given date.</param>
    /// <returns>The constructed log file name.</returns>
    public string BuildCustomFileName(DateTime date, int index)
    {
        string baseName = LogFileName;

        if (FormatLogFileName != null)
        {
            baseName = FormatLogFileName(baseName);
        }

        string ext = Path.GetExtension(baseName);
        string stem = Path.GetFileNameWithoutExtension(baseName);
        string datePart = date.ToString("yy_MM_dd", CultureInfo.InvariantCulture);
        string newName = $"{stem}_{datePart}_{index}{ext}";

        if (UsePerProcessSuffix)
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
