using Nalix.Logging.Exceptions;

namespace Nalix.Logging.Options;

/// <summary>
/// Configuration options for the file logger.
/// </summary>
public sealed class FileLogOptions
{
    #region Constants

    // Default values
    private const System.Int32 DefaultMaxFileSize = 10 * 1024 * 1024; // 10 MB

    private const System.Int32 DefaultMaxQueueSize = 4096;
    private const System.Boolean DefaultAppendToFile = true;

    #endregion Constants

    #region Fields

    private static readonly System.TimeSpan DefaultFlushInterval = System.TimeSpan.FromSeconds(1);

    private static readonly System.String DefaultBaseDirectory =
        System.AppDomain.CurrentDomain.BaseDirectory.TrimEnd(System.IO.Path.DirectorySeparatorChar);

    private static readonly System.String DefaultLogDirectory =
        System.IO.Path.Combine(DefaultBaseDirectory, "assets", "data", "logs");

    private System.Int32 _maxFileSize = DefaultMaxFileSize;
    private System.Int32 _maxQueueSize = DefaultMaxQueueSize;
    private System.String _logFileName = $"log_{System.Environment.MachineName}_.log";

    #endregion Fields

    #region Properties

    /// <summary>
    /// Specifies whether to append to existing log files or overwrite them.
    /// </summary>
    public System.Boolean Append { get; set; } = DefaultAppendToFile;

    /// <summary>
    /// The maximum allowed file size for a log file in bytes.
    /// When this size is reached, a new log file will be created.
    /// </summary>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown when value is less than 1KB or greater than 2GB.</exception>
    public System.Int32 MaxFileSizeBytes
    {
        get => _maxFileSize;
        set
        {
            const System.Int32 min = 1024; // 1 KB minimum
            const System.Int32 max = System.Int32.MaxValue; // 2 GB maximum

            if (value is < min or > max)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(value), $"Value must be between {min} and {max} bytes");
            }

            _maxFileSize = value;
        }
    }

    /// <summary>
    /// The maximum TransportProtocol of entries that can be queued before blocking or discarding.
    /// </summary>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown when value is less than 1.</exception>
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
    /// The actual filename may have additional information appended like date or sequence TransportProtocol.
    /// </remarks>
    public System.String LogFileName
    {
        get => _logFileName;
        set => _logFileName = System.String.IsNullOrWhiteSpace(value)
            ? $"log_{System.Environment.MachineName}_.log"
            : value;
    }

    /// <summary>
    /// Gets or sets the directory where log files will be stored.
    /// </summary>
    /// <remarks>
    /// If the directory doesn't exist, it will be created when logging starts.
    /// </remarks>
    public System.String LogDirectory { get; set; } = DefaultLogDirectory;

    /// <summary>
    /// Gets or sets the interval at which log entries are flushed to disk.
    /// </summary>
    /// <remarks>
    /// A shorter interval reduces the risk of data loss but may impact performance.
    /// </remarks>
    public System.TimeSpan FlushInterval { get; set; } = DefaultFlushInterval;

    /// <summary>
    /// Gets or sets whether to include the date in log file names.
    /// </summary>
    public System.Boolean IncludeDateInFileName { get; set; } = true;

    /// <summary>
    /// A custom formatter for the log file name.
    /// </summary>
    /// <remarks>
    /// By providing a custom formatter, you can define your own criteria for generating log file names.
    /// This formatter is applied once when creating a new log file, not for every log entry.
    /// </remarks>
    public System.Func<System.String, System.String>? FormatLogFileName { get; set; }

    /// <summary>
    /// A custom handler for file errors.
    /// </summary>
    /// <remarks>
    /// If this handler is provided, exceptions occurring during file operations will be passed to it.
    /// You can handle file errors according to your application's logic and propose an alternative log file name.
    /// </remarks>
    public System.Action<FileError>? HandleFileError { get; set; }

    /// <summary>
    /// Gets or sets whether to use background thread for file operations.
    /// </summary>
    public System.Boolean UseBackgroundThread { get; set; } = true;

    /// <summary>
    /// Gets or sets the behavior when the queue is full.
    /// </summary>
    /// <remarks>
    /// When true, logging will block until queue space is available.
    /// When false, log entries will be discarded when the queue is full.
    /// </remarks>
    public System.Boolean BlockWhenQueueFull { get; set; } = false;

    #endregion Properties

    #region Methods

    /// <summary>
    /// Gets full path to the current log file.
    /// </summary>
    public System.String GetFullLogFilePath() => System.IO.Path.Combine(LogDirectory, LogFileName);

    #endregion Methods
}
