using Nalix.Logging.Exceptions;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace Nalix.Logging.Engine.Formatters.Internal.Files;

/// <summary>
/// Manages writing logs to a file with support for file rotation and error handling.
/// </summary>
internal sealed class FileWriter : IDisposable
{
    // Set buffer size to 8KB for optimal performance
    private const int WriteBufferSize = 8 * 1024;

    private readonly FileLoggerProvider _provider;
    private readonly Lock _fileLock = new();

    private bool _isDisposed;
    private int _fileCounter;
    private long _currentFileSize;
    private FileStream? _logFileStream;
    private StreamWriter? _logFileWriter;

    /// <summary>
    /// Initializes a new instance of <see cref="FileWriter"/>.
    /// </summary>
    /// <param name="provider">The file logger provider.</param>
    /// <exception cref="ArgumentNullException">Thrown if provider is null.</exception>
    internal FileWriter(FileLoggerProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));

        // Create the directory if it doesn't exist
        EnsureDirectoryExists();

        // Open or create the initial log file
        OpenFile(provider.Append);
    }

    /// <summary>
    /// Ensures the log directory exists.
    /// </summary>
    private void EnsureDirectoryExists()
    {
        try
        {
            var directory = _provider.Options.LogDirectory;
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error creating log directory: {ex.Message}");

            // Try to use a fallback directory in case of permission issues
            try
            {
                var tempPath = Path.Combine(Path.GetTempPath(), "Nalix", "Logs");
                Directory.CreateDirectory(tempPath);
                _provider.Options.LogDirectory = tempPath;
            }
            catch
            {
                // Last resort - use current directory
                _provider.Options.LogDirectory = ".";
            }
        }
    }

    /// <summary>
    /// Generates a unique log file name.
    /// </summary>
    private string GenerateUniqueLogFileName()
    {
        string baseFileName = _provider.Options.LogFileName;
        string extension = Path.GetExtension(baseFileName);
        string fileNameWithoutExt = Path.GetFileNameWithoutExtension(baseFileName);

        // Start with the original file name
        string fileName = baseFileName;

        // Apply custom formatter if provided
        if (_provider.FormatLogFileName != null)
        {
            try
            {
                fileName = _provider.FormatLogFileName(baseFileName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error applying custom file name formatter: {ex.Message}");
            }
        }
        // Otherwise apply the default date-based naming
        else if (_provider.Options.IncludeDateInFileName)
        {
            var now = DateTime.Now; // Use local time for file names
            fileName = $"{fileNameWithoutExt}_{now:yyyy-MM-dd}_{_fileCounter++}{extension}";
        }

        string logDirectory = _provider.Options.LogDirectory;
        string fullPath = Path.Combine(logDirectory, fileName);

        // Ensure file name uniqueness by adding counter if file exists
        int uniqueCounter = 0;
        while (File.Exists(fullPath))
        {
            uniqueCounter++;
            string uniqueName = $"{fileNameWithoutExt}_{DateTime.Now:yyyy-MM-dd}_{_fileCounter}_{uniqueCounter}{extension}";
            fullPath = Path.Combine(logDirectory, uniqueName);

            // Safety check to avoid infinite loop
            if (uniqueCounter > 9999)
            {
                fullPath = Path.Combine(logDirectory,
                    $"{fileNameWithoutExt}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}_{Guid.NewGuid().ToString()[..8]}{extension}");
                break;
            }
        }

        return Path.GetFileName(fullPath);
    }

    /// <summary>
    /// Creates a new log file when the existing one exceeds size limits.
    /// </summary>
    private void CreateNewLogFile()
    {
        lock (_fileLock)
        {
            Close(); // Ensure the old file is closed properly
            string newFileName = GenerateUniqueLogFileName();
            _provider.Options.LogFileName = newFileName;
            OpenFile(false); // Open new file in create mode
        }
    }

    /// <summary>
    /// Creates and opens the log file stream.
    /// </summary>
    /// <param name="append">Whether to append to an existing file.</param>
    private void CreateLogFileStream(bool append)
    {
        string logFilePath = Path.Combine(_provider.Options.LogDirectory, _provider.Options.LogFileName);

        try
        {
            // Check if file exists and get its size
            bool fileExists = File.Exists(logFilePath);
            _currentFileSize = fileExists ? new FileInfo(logFilePath).Length : 0;

            // If file exists, is non-empty, and exceeds size limit, create a new one instead
            if (fileExists && _currentFileSize > 0 && _currentFileSize >= _provider.MaxFileSize)
            {
                CreateNewLogFile();
                return;
            }

            // Create the file stream with appropriate sharing mode
            _logFileStream = new FileStream(
                logFilePath,
                append ? FileMode.Append : FileMode.Create,
                FileAccess.Write,
                FileShare.Read,
                WriteBufferSize,
                FileOptions.WriteThrough);

            // Create the writer with appropriate encoding and buffer
            _logFileWriter = new StreamWriter(_logFileStream, Encoding.UTF8, WriteBufferSize)
            {
                AutoFlush = false // We'll control flushing explicitly
            };

            // Write a header for new files
            if (!append || _currentFileSize == 0)
            {
                var headerBuilder = new StringBuilder();
                headerBuilder.AppendLine("-----------------------------------------------------");
                headerBuilder.AppendLine($"Log Files Created: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                headerBuilder.AppendLine($"User: {Environment.UserName}");
                headerBuilder.AppendLine($"Machine: {Environment.MachineName}");
                headerBuilder.AppendLine($"OS: {Environment.OSVersion}");
                headerBuilder.AppendLine("-----------------------------------------------------");

                _logFileWriter.WriteLine(headerBuilder.ToString());
                _logFileWriter.Flush();

                // Update current file size to include header
                _currentFileSize = _logFileStream.Length;
            }
        }
        catch (Exception ex)
        {
            // Let the provider handle file errors
            _provider.HandleFileError?.Invoke(new FileError(ex, logFilePath));

            // Clean up any partially created resources
            _logFileWriter?.Dispose();
            _logFileStream?.Dispose();
            _logFileWriter = null;
            _logFileStream = null;

            throw; // Re-throw for provider to handle
        }
    }

    /// <summary>
    /// Opens a log file.
    /// </summary>
    /// <param name="append">Whether to append to an existing file.</param>
    private void OpenFile(bool append)
    {
        lock (_fileLock)
        {
            CreateLogFileStream(append);
        }
    }

    /// <summary>
    /// Use a new log file, typically after an error with the current one.
    /// </summary>
    /// <param name="newLogFileName">New log file name.</param>
    internal void UseNewLogFile(string newLogFileName)
    {
        if (string.IsNullOrEmpty(newLogFileName))
            throw new ArgumentException("New log file name cannot be empty", nameof(newLogFileName));

        lock (_fileLock)
        {
            Close(); // Close the current file first
            _provider.Options.LogFileName = Path.GetFileName(newLogFileName);
            OpenFile(_provider.Append);
        }
    }

    /// <summary>
    /// Writes a message to the log file.
    /// </summary>
    /// <param name="message">The message to write.</param>
    /// <param name="flush">Whether to flush after writing.</param>
    internal void WriteMessage(string message, bool flush)
    {
        if (string.IsNullOrEmpty(message))
            return;

        lock (_fileLock)
        {
            // If file is not open, try to open it
            if (_logFileWriter == null || _logFileStream == null)
            {
                OpenFile(_provider.Append);

                // If still null after attempted recovery, give up on this message
                if (_logFileWriter == null || _logFileStream == null)
                    return;
            }

            // Estimate message size (approximate for performance)
            int messageSize = message.Length * sizeof(char) + Environment.NewLine.Length * sizeof(char);

            // Check if adding this message would exceed the file size limit
            if (_currentFileSize + messageSize > _provider.MaxFileSize)
            {
                CreateNewLogFile();

                // If file creation failed, give up on this message
                if (_logFileWriter == null || _logFileStream == null)
                    return;
            }

            // Write the message
            _logFileWriter.WriteLine(message);

            // Update the current file size (approximate)
            _currentFileSize += messageSize;

            // Flush if requested or if we're approaching the buffer size
            if (flush)
            {
                _logFileWriter.Flush();
            }
        }
    }

    /// <summary>
    /// Forces any buffered data to be written to the file.
    /// </summary>
    internal void Flush()
    {
        lock (_fileLock)
        {
            _logFileWriter?.Flush();
        }
    }

    /// <summary>
    /// Closes the log file.
    /// </summary>
    internal void Close()
    {
        lock (_fileLock)
        {
            try
            {
                _logFileWriter?.Flush();
                _logFileWriter?.Dispose();
                _logFileStream?.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error closing log file: {ex.Message}");
            }
            finally
            {
                _logFileWriter = null;
                _logFileStream = null;
                _currentFileSize = 0;
            }
        }
    }

    /// <summary>
    /// Disposes the writer and releases all resources.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        Close();
    }
}
