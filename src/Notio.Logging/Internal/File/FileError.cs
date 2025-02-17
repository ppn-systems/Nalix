using System;

namespace Notio.Logging.Internal.File;

/// <summary>
/// Represents the file error context.
/// </summary>
public class FileError
{
    internal string? NewLogFileName { get; private set; }

    internal FileError(Exception ex)
    {
    }

    /// <summary>
    /// Suggest a new log file name to use in place of the current one.
    /// </summary>
    /// <param name="newLogFileName">New log file name</param>
    public void UseNewLogFileName(string newLogFileName)
        => NewLogFileName = newLogFileName;
}
