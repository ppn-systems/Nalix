using System;
using System.IO;

namespace Notio.Logging.Internal.File;

/// <summary>
/// Manages writing logs to a file.
/// </summary>
internal class FileWriter
{
    private int _count;
    private FileStream? _logFileStream;
    private StreamWriter? _logFileWriter;
    private readonly FileLoggerProvider _fileLogProvider;

    /// <summary>
    /// Initializes a new instance of <see cref="FileWriter"/>.
    /// </summary>
    /// <param name="fileLogProvider">The file logger provider.</param>
    internal FileWriter(FileLoggerProvider fileLogProvider)
    {
        _fileLogProvider = fileLogProvider ?? throw new ArgumentNullException(nameof(fileLogProvider));
        OpenFile(_fileLogProvider.Append);
    }

    /// <summary>
    /// Generates a unique log file name.
    /// </summary>
    private string GenerateUniqueLogFileName()
    {
        string baseFileName = _fileLogProvider.Options.LogFileName;
        string logDirectory = _fileLogProvider.Options.LogDirectory;
        string newFileName;

        do
        {
            newFileName = $"{baseFileName}_{_count++}.log";
        } while (System.IO.File.Exists(Path.Combine(logDirectory, newFileName)));

        return newFileName;
    }

    /// <summary>
    /// Creates a new log file when the existing one exceeds size limits.
    /// </summary>
    private void CreateNewLogFile()
    {
        Close(); // Ensure the old file is closed properly
        string newFileName = GenerateUniqueLogFileName();
        _fileLogProvider.Options.LogFileName = newFileName;
        CreateLogFileStream(false);
    }

    /// <summary>
    /// Creates and opens the log file stream.
    /// </summary>
    private void CreateLogFileStream(bool append)
    {
        try
        {
            string logFilePath = Path.Combine(_fileLogProvider.Options.LogDirectory, _fileLogProvider.Options.LogFileName);
            Directory.CreateDirectory(Path.GetDirectoryName(logFilePath)!);

            // If the file already exists and is too large, create a new one before opening
            if (System.IO.File.Exists(logFilePath) && new FileInfo(logFilePath).Length >= _fileLogProvider.MaxFileSize)
            {
                CreateNewLogFile();
                return;
            }

            _logFileStream = new FileStream(logFilePath, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.Read);
            _logFileWriter = new StreamWriter(_logFileStream) { AutoFlush = true };
        }
        catch (Exception ex)
        {
            _fileLogProvider.HandleFileError?.Invoke(new FileError(ex));
        }
    }

    /// <summary>
    /// Opens a log file.
    /// </summary>
    private void OpenFile(bool append) => CreateLogFileStream(append);

    /// <summary>
    /// Use the new log file.
    /// </summary>
    /// <param name="newLogFileName">New log file name.</param>
    internal void UseNewLogFile(string newLogFileName)
    {
        _fileLogProvider.Options.LogFileName = Path.GetFileName(newLogFileName);
        CreateLogFileStream(_fileLogProvider.Append);
    }

    /// <summary>
    /// Writes a message to the log file.
    /// </summary>
    internal void WriteMessage(string message, bool flush)
    {
        if (_logFileStream == null || _logFileWriter == null) return;

        _logFileWriter.WriteLine(message);
        if (flush) _logFileWriter.Flush();

        // Check if file exceeded max size and rotate if necessary
        if (_logFileStream.Length >= _fileLogProvider.MaxFileSize)
        {
            CreateNewLogFile();
        }
    }

    /// <summary>
    /// Closes the log file.
    /// </summary>
    internal void Close()
    {
        _logFileWriter?.Dispose();
        _logFileStream?.Dispose();
        _logFileWriter = null;
        _logFileStream = null;
    }
}
