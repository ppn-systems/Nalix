using Notio.Logging.Targets;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;

namespace Notio.Logging.Internal.File;

/// <summary>
/// A provider for general file logging.
/// </summary>
public class FileLoggerProvider : IDisposable
{
    private readonly ConcurrentDictionary<string, FileLoggingTarget> _loggers = new();
    private readonly BlockingCollection<string> _entryQueue = new(1024);
    private readonly Task _processQueueTask;
    private readonly FileWriter _fWriter;

    internal FileLoggerOptions Options { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the log file will be appended to (true) or overwritten (false).
    /// </summary>
    public bool Append => Options.Append;

    /// <summary>
    /// Gets the maximum size of the log file before it rolls over.
    /// </summary>
    public int MaxFileSize => Options.MaxFileSize;

    /// <summary>
    /// Custom formatter for log file names.
    /// </summary>
    public Func<string, string>? FormatLogFileName
    {
        get => Options.FormatLogFileName;
        set => Options.FormatLogFileName = value;
    }

    /// <summary>
    /// Custom handler for file errors.
    /// </summary>
    public Action<FileError>? HandleFileError
    {
        get => Options.HandleFileError;
        set => Options.HandleFileError = value;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="FileLoggerProvider"/> with the specified file name and configuration options.
    /// </summary>
    /// <param name="options">The configuration options.</param>
    public FileLoggerProvider(FileLoggerOptions options)
    {
        Options = options;

        _fWriter = new FileWriter(this);
        _processQueueTask = Task.Factory.StartNew(
            ProcessQueue,
            this,
            TaskCreationOptions.LongRunning);
    }

    /// <summary>
    /// Releases the resources used by the <see cref="FileLoggerProvider"/>.
    /// </summary>
    public void Dispose()
    {
        _entryQueue.CompleteAdding();
        try
        {
            _processQueueTask.Wait(1500);  // similar to ConsoleLogger
        }
        catch (TaskCanceledException)
        {
        }
        catch (AggregateException ex) when (ex.InnerExceptions is [TaskCanceledException]) { }

        _loggers.Clear();
        _fWriter.Close();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Writes a log entry to the queue.
    /// </summary>
    /// <param name="message">The log message.</param>
    internal void WriteEntry(string message)
    {
        if (!_entryQueue.IsAddingCompleted)
        {
            try
            {
                _entryQueue.Add(message);
            }
            catch (InvalidOperationException) { }
        }
        // Do nothing if unable to add message
    }

    private void ProcessQueue()
    {
        var writeMessageFailed = false;
        foreach (var message in _entryQueue.GetConsumingEnumerable())
        {
            try
            {
                if (!writeMessageFailed)
                    _fWriter.WriteMessage(message, _entryQueue.Count == 0);
            }
            catch (Exception ex)
            {
                // Handle errors if 'HandleFileError' is provided
                var stopLogging = true;
                if (HandleFileError != null)
                {
                    var fileErr = new FileError(ex);
                    try
                    {
                        HandleFileError(fileErr);

                        if (fileErr.NewLogFileName != null)
                        {
                            _fWriter.UseNewLogFile(fileErr.NewLogFileName);
                        }
                        else
                        {
                            string fallbackFile = Path.Combine("Logs", "backup_" + DateTime.UtcNow.Ticks);
                            fileErr.UseNewLogFileName(fallbackFile);
                            _fWriter.UseNewLogFile(fallbackFile);
                        }

                        // Write the failed message to the new log file
                        _fWriter.WriteMessage(message, _entryQueue.Count == 0);
                        stopLogging = false;
                    }
                    catch
                    {
                        // Ignore errors in HandleFileError or invalid file name proposals
                    }
                }
                if (stopLogging)
                {
                    // Stop processing log messages as we cannot write to the log file
                    _entryQueue.CompleteAdding();
                    writeMessageFailed = true;
                }
            }
        }
    }

    private static void ProcessQueue(object? state)
    {
        if (state == null)
            return;

        FileLoggerProvider fileLogger = (FileLoggerProvider)state;
        fileLogger.ProcessQueue();
    }
}
