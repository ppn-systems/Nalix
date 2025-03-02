using System;
using System.IO;

namespace Notio.Logging.Internal.File;

/// <summary>
/// Configuration options for the file logger.
/// </summary>
public sealed class FileLoggerOptions
{
    // Default values
    private const int DefaultMaxFileSize = 10 * 1024 * 1024; // 10 MB
    private const int DefaultMaxQueueSize = 4096;
    private const bool DefaultAppendToFile = true;
    private static readonly TimeSpan DefaultFlushInterval = TimeSpan.FromSeconds(1);

    private static readonly string DefaultBaseDirectory = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
    private static readonly string DefaultLogDirectory = Path.Combine(DefaultBaseDirectory, "Logs");

    private int _maxFileSize = DefaultMaxFileSize;
    private int _maxQueueSize = DefaultMaxQueueSize;
    private string _logFileName = $"log_{Environment.MachineName}_.log";

    /// <summary>
    /// Specifies whether to append to existing log files or overwrite them.
    /// </summary>
    public bool Append { get; set; } = DefaultAppendToFile;

    /// <summary>
    /// The maximum allowed file size for a log file in bytes.
    /// When this size is reached, a new log file will be created.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when value is less than 1KB or greater than 2GB.</exception>
    public int MaxFileSizeBytes
    {
        get => _maxFileSize;
        set
        {
            const int min = 1024; // 1 KB minimum
            const int max = int.MaxValue; // 2 GB maximum

            if (value < min || value > max)
                throw new ArgumentOutOfRangeException(nameof(value), $"Value must be between {min} and {max} bytes");

            _maxFileSize = value;
        }
    }

    /// <summary>
    /// The maximum number of entries that can be queued before blocking or discarding.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when value is less than 1.</exception>
    public int MaxQueueSize
    {
        get => _maxQueueSize;
        set
        {
            if (value < 1)
                throw new ArgumentOutOfRangeException(nameof(value), "Queue size must be at least 1");

            _maxQueueSize = value;
        }
    }

    /// <summary>
    /// Gets or sets the name template for log files.
    /// </summary>
    /// <remarks>
    /// The actual filename may have additional information appended like date or sequence number.
    /// </remarks>
    public string LogFileName
    {
        get => _logFileName;
        set => _logFileName = string.IsNullOrWhiteSpace(value)
            ? $"log_{Environment.MachineName}_.log"
            : value;
    }

    /// <summary>
    /// Gets or sets the directory where log files will be stored.
    /// </summary>
    /// <remarks>
    /// If the directory doesn't exist, it will be created when logging starts.
    /// </remarks>
    public string LogDirectory { get; set; } = DefaultLogDirectory;

    /// <summary>
    /// Gets or sets the interval at which log entries are flushed to disk.
    /// </summary>
    /// <remarks>
    /// A shorter interval reduces the risk of data loss but may impact performance.
    /// </remarks>
    public TimeSpan FlushInterval { get; set; } = DefaultFlushInterval;

    /// <summary>
    /// Gets or sets whether to include the date in log file names.
    /// </summary>
    public bool IncludeDateInFileName { get; set; } = true;

    /// <summary>
    /// A custom formatter for the log file name.
    /// </summary>
    /// <remarks>
    /// By providing a custom formatter, you can define your own criteria for generating log file names.
    /// This formatter is applied once when creating a new log file, not for every log entry.
    /// </remarks>
    public Func<string, string>? FormatLogFileName { get; set; }

    /// <summary>
    /// A custom handler for file errors.
    /// </summary>
    /// <remarks>
    /// If this handler is provided, exceptions occurring during file operations will be passed to it.
    /// You can handle file errors according to your application's logic and propose an alternative log file name.
    /// </remarks>
    public Action<FileError>? HandleFileError { get; set; }

    /// <summary>
    /// Gets or sets whether to use background thread for file operations.
    /// </summary>
    public bool UseBackgroundThread { get; set; } = true;

    /// <summary>
    /// Gets or sets the behavior when the queue is full.
    /// </summary>
    /// <remarks>
    /// When true, logging will block until queue space is available.
    /// When false, log entries will be discarded when the queue is full.
    /// </remarks>
    public bool BlockWhenQueueFull { get; set; } = false;

    /// <summary>
    /// Gets full path to the current log file.
    /// </summary>
    public string GetFullLogFilePath() => Path.Combine(LogDirectory, LogFileName);
}
