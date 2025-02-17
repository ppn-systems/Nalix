using System;

namespace Notio.Logging.Internal.File;

/// <summary>
/// Represents the file error context.
/// </summary>
public class FileError
{
    /// <summary>
    /// An exception occurred during a file operation.
    /// </summary>
    public Exception ErrorException { get; private set; }

    /// <summary>
    /// Current log file name.
    /// </summary>
    public string LogFileName { get; private set; }

    internal string? NewLogFileName { get; private set; }

    internal FileError(string logFileName, Exception ex)
    {
        LogFileName = logFileName;
        ErrorException = ex;
    }

    /// <summary>
    /// Suggest a new log file name to use in place of the current one.
    /// </summary>
    /// <param name="newLogFileName">New log file name</param>
    public void UseNewLogFileName(string newLogFileName)
        => NewLogFileName = newLogFileName;
}
