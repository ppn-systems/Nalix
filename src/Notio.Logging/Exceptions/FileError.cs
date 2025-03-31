using System;

namespace Notio.Logging.Exceptions;

/// <summary>
/// Represents an error that occurred during file logging operations.
/// Contains context information and recovery options.
/// </summary>
/// <remarks>
/// Creates a new file error instance with detailed context.
/// </remarks>
/// <param name="ex">The exception that caused the error.</param>
/// <param name="filePath">The file path where the error occurred.</param>
public sealed class FileError(Exception ex, string filePath)
{
    /// <summary>
    /// Gets or sets the new log file name to use when recovering from errors.
    /// </summary>
    public string NewLogFileName { get; set; } = string.Empty;

    /// <summary>
    /// Gets the original log file path where the error occurred.
    /// </summary>
    public string OriginalFilePath { get; } = filePath ?? string.Empty;

    /// <summary>
    /// Gets the exception that caused the file error.
    /// </summary>
    public Exception Exception { get; } = ex ?? throw new ArgumentNullException(nameof(ex));
}
