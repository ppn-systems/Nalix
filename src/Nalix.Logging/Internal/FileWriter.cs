// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Logging.Internal.Exceptions;

namespace Nalix.Logging.Internal;

/// <summary>
/// Manages writing logs to a file with support for file rotation and error handling.
/// </summary>
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[System.Diagnostics.DebuggerDisplay("File={_provider.Options.LogFileName,nq}, Size={_currentFileSize}")]
internal sealed class FileWriter : System.IDisposable
{
    #region Fields

    // Set buffer size to 8KB for optimal performance
    private const System.Int32 WriteBufferSize = 8 * 1024;

    private readonly FileLoggerProvider _provider;
    private readonly System.Threading.Lock _fileLock = new();

    private System.Boolean _isDisposed;
    private System.Int32 _fileCounter;
    private System.Int64 _currentFileSize;
    private System.IO.FileStream? _logFileStream;
    private System.IO.StreamWriter? _logFileWriter;

    #endregion Fields

    #region Constructor

    /// <summary>
    /// Initializes a new instance of <see cref="FileWriter"/>.
    /// </summary>
    /// <param name="provider">The file logger provider.</param>
    /// <exception cref="System.ArgumentNullException">Thrown if provider is null.</exception>
    internal FileWriter(FileLoggerProvider provider)
    {
        _provider = provider ?? throw new System.ArgumentNullException(nameof(provider));

        // Create the directory if it doesn't exist
        EnsureDirectoryExists();

        // Open or create the initial log file
        OpenFile(provider.Append);
    }

    #endregion Constructor

    #region APIs

    /// <summary>
    /// Use a new log file, typically after an error with the current one.
    /// </summary>
    /// <param name="newLogFileName">New log file name.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal void UseNewLogFile(System.String newLogFileName)
    {
        if (System.String.IsNullOrEmpty(newLogFileName))
        {
            throw new System.ArgumentException("New log file name cannot be empty", nameof(newLogFileName));
        }

        lock (_fileLock)
        {
            Close(); // Close the current file first
            _provider.Options.LogFileName = System.IO.Path.GetFileName(newLogFileName);
            OpenFile(_provider.Append);
        }
    }

    /// <summary>
    /// Writes a message to the log file.
    /// </summary>
    /// <param name="message">The message to write.</param>
    /// <param name="flush">Whether to flush after writing.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal void WriteMessage(System.String message, System.Boolean flush)
    {
        if (System.String.IsNullOrEmpty(message))
        {
            return;
        }

        lock (_fileLock)
        {
            // If file is not open, try to open it
            if (_logFileWriter == null || _logFileStream == null)
            {
                OpenFile(_provider.Append);

                // If still null after attempted recovery, give up on this message
                if (_logFileWriter == null || _logFileStream == null)
                {
                    return;
                }
            }

            // Estimate message size (approximate for performance)
            System.Int32 messageSize = (message.Length * sizeof(System.Char)) +
                (System.Environment.NewLine.Length * sizeof(System.Char));

            // Check if adding this message would exceed the file size limit
            if (_currentFileSize + messageSize > _provider.MaxFileSize)
            {
                CreateNewLogFile();

                // If file creation failed, give up on this message
                if (_logFileWriter == null || _logFileStream == null)
                {
                    return;
                }
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
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
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Error closing log file: {ex.Message}");
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        Close();
    }

    #endregion APIs

    #region Private Methods

    /// <summary>
    /// Ensures the log directory exists.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private void EnsureDirectoryExists()
    {
        try
        {
            System.String directory = _provider.Options.LogDirectory;
            if (!System.IO.Directory.Exists(directory))
            {
                _ = System.IO.Directory.CreateDirectory(directory);
            }
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Error creating log directory: {ex.Message}");

            // Try to use a fallback directory in case of permission issues
            try
            {
                System.String tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "assets", "logs");
                _ = System.IO.Directory.CreateDirectory(tempPath);
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private System.String GenerateUniqueLogFileName()
    {
        System.String baseFileName = _provider.Options.LogFileName;
        System.String extension = System.IO.Path.GetExtension(baseFileName);
        System.String fileNameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(baseFileName);

        // Start with the original file name
        System.String fileName = baseFileName;

        // Apply custom formatter if provided
        if (_provider.FormatLogFileName != null)
        {
            try
            {
                fileName = _provider.FormatLogFileName(baseFileName);
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Error applying custom file name formatter: {ex.Message}");
            }
        }
        // Otherwise apply the default date-based naming
        else if (_provider.Options.IncludeDateInFileName)
        {
            System.DateTime now = System.DateTime.Now; // Use local time for file names
            fileName = $"{fileNameWithoutExt}_{now:yyyy-MM-dd}_{_fileCounter++}{extension}";
        }

        System.String logDirectory = _provider.Options.LogDirectory;
        System.String fullPath = System.IO.Path.Combine(logDirectory, fileName);

        // Ensure file name uniqueness by adding counter if file exists
        System.Int32 uniqueCounter = 0;
        while (System.IO.File.Exists(fullPath))
        {
            uniqueCounter++;
            System.String uniqueName = $"{fileNameWithoutExt}_" +
                $"{System.DateTime.Now:yyyy-MM-dd}_" +
                $"{_fileCounter}_" +
                $"{uniqueCounter}{extension}";

            fullPath = System.IO.Path.Combine(logDirectory, uniqueName);

            // Safety check to avoid infinite loop
            if (uniqueCounter > 9999)
            {
                fullPath = System.IO.Path.Combine(logDirectory,
                    $"{fileNameWithoutExt}_" +
                    $"{System.DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}_" +
                    $"{System.Guid.NewGuid().ToString()[..8]}{extension}");
                break;
            }
        }

        return System.IO.Path.GetFileName(fullPath);
    }

    /// <summary>
    /// Creates a new log file when the existing one exceeds size limits.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private void CreateNewLogFile()
    {
        lock (_fileLock)
        {
            Close(); // Ensure the old file is closed properly
            System.String newFileName = GenerateUniqueLogFileName();
            _provider.Options.LogFileName = newFileName;
            OpenFile(false); // Open new file in create mode
        }
    }

    /// <summary>
    /// Creates and opens the log file stream.
    /// </summary>
    /// <param name="append">Whether to append to an existing file.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private void CreateLogFileStream(System.Boolean append)
    {
        System.String logFilePath = System.IO.Path.Combine(_provider.Options.LogDirectory, _provider.Options.LogFileName);

        try
        {
            // Check if file exists and get its size
            System.Boolean fileExists = System.IO.File.Exists(logFilePath);
            _currentFileSize = fileExists ? new System.IO.FileInfo(logFilePath).Length : 0;

            // If file exists, is non-empty, and exceeds size limit, create a new one instead
            if (fileExists && _currentFileSize > 0 && _currentFileSize >= _provider.MaxFileSize)
            {
                CreateNewLogFile();
                return;
            }

            // Create the file stream with appropriate sharing mode
            _logFileStream = new System.IO.FileStream(
                logFilePath,
                append ? System.IO.FileMode.Append : System.IO.FileMode.Create,
                System.IO.FileAccess.Write,
                System.IO.FileShare.Read,
                WriteBufferSize,
                System.IO.FileOptions.WriteThrough);

            // Create the writer with appropriate encoding and buffer
            _logFileWriter = new System.IO.StreamWriter(_logFileStream, System.Text.Encoding.UTF8, WriteBufferSize)
            {
                AutoFlush = false // We'll control flushing explicitly
            };

            // Write a header for new files
            if (!append || _currentFileSize == 0)
            {
                System.Text.StringBuilder headerBuilder = new();
                _ = headerBuilder.AppendLine("-----------------------------------------------------");
                _ = headerBuilder.AppendLine($"Log Files Created: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                _ = headerBuilder.AppendLine($"User: {System.Environment.UserName}");
                _ = headerBuilder.AppendLine($"Machine: {System.Environment.MachineName}");
                _ = headerBuilder.AppendLine($"OS: {System.Environment.OSVersion}");
                _ = headerBuilder.AppendLine("-----------------------------------------------------");

                _logFileWriter.WriteLine(headerBuilder.ToString());
                _logFileWriter.Flush();

                // Update current file size to include header
                _currentFileSize = _logFileStream.Length;
            }
        }
        catch (System.Exception ex)
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private void OpenFile(System.Boolean append)
    {
        lock (_fileLock)
        {
            CreateLogFileStream(append);
        }
    }

    #endregion Private Methods
}
