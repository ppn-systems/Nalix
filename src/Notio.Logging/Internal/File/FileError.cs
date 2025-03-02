using System;
using System.IO;
using System.Linq;

namespace Notio.Logging.Internal.File;

/// <summary>
/// Represents an error that occurred during file logging operations.
/// Contains context information and recovery options.
/// </summary>
public sealed class FileError
{
    /// <summary>
    /// Gets the exception that caused the file error.
    /// </summary>
    public Exception Exception { get; }

    /// <summary>
    /// Gets the original log file path where the error occurred.
    /// </summary>
    public string OriginalFilePath { get; }

    /// <summary>
    /// Gets or sets the new log file name to use when recovering from errors.
    /// </summary>
    internal string? NewLogFileName { get; private set; }

    /// <summary>
    /// Creates a new file error instance with detailed context.
    /// </summary>
    /// <param name="ex">The exception that caused the error.</param>
    /// <param name="filePath">The file path where the error occurred.</param>
    internal FileError(Exception ex, string filePath)
    {
        Exception = ex ?? throw new ArgumentNullException(nameof(ex));
        OriginalFilePath = filePath ?? string.Empty;
    }

    /// <summary>
    /// Suggests a new log file name to use in place of the current one.
    /// </summary>
    /// <param name="newLogFileName">New log file name or path.</param>
    /// <exception cref="ArgumentException">Thrown when the provided file name is null or empty.</exception>
    public void UseNewLogFileName(string newLogFileName)
    {
        if (string.IsNullOrWhiteSpace(newLogFileName))
            throw new ArgumentException("New log file name cannot be null or empty", nameof(newLogFileName));

        // Clean any potentially problematic characters from the path
        var safeName = Path.GetInvalidFileNameChars()
            .Aggregate(newLogFileName, (current, c) => current.Replace(c, '_'));

        NewLogFileName = safeName;
    }
}
