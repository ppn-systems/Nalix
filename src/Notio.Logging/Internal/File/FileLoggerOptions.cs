using Notio.Common.Enums;
using System;
using System.IO;

namespace Notio.Logging.Internal.File;

/// <summary>
/// Configuration options for the file logger.
/// </summary>
public class FileLoggerOptions
{
    private static readonly string _baseDirectory = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);

    /// <summary>
    /// Specifies whether to append to existing log files or overwrite them.
    /// </summary>
    public bool Append { get; set; } = true;

    /// <summary>
    /// The maximum allowed file size for a log file.
    /// </summary>
    public int MaxFileSize = 10 * 1024 * 1024;

    /// <summary>
    /// The file size limit in bytes.
    /// </summary>
    /// <remarks>
    /// If a file size limit is set, the logger will create a new log file once the limit is reached.
    /// </remarks>
    public int FileSizeLimitBytes { get; set; } = 3;

    /// <summary>
    /// Gets or sets the name of the log file.
    /// </summary>
    public string LogFileName { get; set; } = "logging_";

    /// <summary>
    /// Gets or sets the directory where log files will be stored.
    /// </summary>
    public string LogDirectory { get; set; } = Path.Combine(_baseDirectory, "Logs");

    /// <summary>
    /// The minimum logging level for the file logger.
    /// </summary>
    public LoggingLevel MinLevel { get; set; } = LoggingLevel.Trace;

    /// <summary>
    /// A custom formatter for the log file name.
    /// </summary>
    /// <remarks>
    /// By providing a custom formatter, you can define your own criteria for generating log file names.
    /// Note that this formatter is invoked each time a log message is written; it is recommended to cache its result to avoid performance issues.
    /// </remarks>
    /// <example>
    /// fileLoggerOpts.FormatLogFileName = (fname) => {
    ///   return String.Formatters(Path.GetFileNameWithoutExtension(fname) + "_{0:yyyy}-{0:MM}-{0:dd}" + Path.GetExtension(fname), TimeStamp.UtcNow);
    /// };
    /// </example>
    public Func<string, string>? FormatLogFileName { get; set; }

    /// <summary>
    /// A custom handler for file errors.
    /// </summary>
    /// <remarks>
    /// If this handler is provided, exceptions occurring during file opening (when creating the <code>FileLoggerProvider</code>)
    /// will be suppressed. You can handle file errors according to your application's logic and propose an alternative log file name
    /// to keep the logger operational.
    /// </remarks>
    /// <example>
    /// fileLoggerOpts.HandleFileError = (err) => {
    ///   err.UseNewLogFileName(Path.GetFileNameWithoutExtension(err.LogFileName) + "_alt" + Path.GetExtension(err.LogFileName));
    /// };
    /// </example>
    public Action<FileError>? HandleFileError { get; set; }
}
