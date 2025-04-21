using Nalix.Logging.Exceptions;
using Nalix.Logging.Options;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Nalix.Logging.Internal.Files;

/// <summary>
/// A high-performance provider for file-based logging with support for file rotation and error handling.
/// </summary>
internal sealed class FileLoggerProvider : IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly BlockingCollection<string> _entryQueue;
    private readonly DateTime _startTime = DateTime.UtcNow;
    private readonly Task? _processQueueTask;
    private readonly FileWriter _fileWriter;
    private readonly int _maxQueueSize;
    private readonly bool _blockWhenQueueFull;
    private bool _isDisposed;

    // Stats for monitoring
    private long _totalEntriesWritten;

    private long _entriesDroppedCount;

    /// <summary>
    /// Gets the configuration options used by this logger provider.
    /// </summary>
    public FileLogOptions Options { get; }

    /// <summary>
    /// Gets a value indicating whether the log file will be appended to (true) or overwritten (false).
    /// </summary>
    public bool Append => Options.Append;

    /// <summary>
    /// Gets the maximum size of the log file in bytes before it rolls over.
    /// </summary>
    public int MaxFileSize => Options.MaxFileSizeBytes;

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
    /// Gets the Number of entries currently in the queue waiting to be written.
    /// </summary>
    public int QueuedEntryCount => _entryQueue.Count;

    /// <summary>
    /// Gets the total Number of log entries written since this provider was created.
    /// </summary>
    public long TotalEntriesWritten => Interlocked.Read(ref _totalEntriesWritten);

    /// <summary>
    /// Gets the Number of entries that were dropped due to queue capacity constraints.
    /// </summary>
    public long EntriesDroppedCount => Interlocked.Read(ref _entriesDroppedCount);

    /// <summary>
    /// Initializes a new instance of <see cref="FileLoggerProvider"/> with the specified configuration options.
    /// </summary>
    /// <param name="options">The configuration options.</param>
    public FileLoggerProvider(FileLogOptions options)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        _maxQueueSize = options.MaxQueueSize;
        _blockWhenQueueFull = options.BlockWhenQueueFull;
        _entryQueue = new BlockingCollection<string>(new ConcurrentQueue<string>(), _maxQueueSize);

        try
        {
            _fileWriter = new FileWriter(this);

            if (options.UseBackgroundThread)
            {
                _processQueueTask = Task.Factory.StartNew(
                    ProcessQueueContinuously,
                    _cancellationTokenSource.Token,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);
            }
            else
            {
                // Create a completed task for non-background mode
                _processQueueTask = Task.CompletedTask;
            }
        }
        catch (Exception ex)
        {
            // Handle initialization errors
            var fileError = new FileError(ex, options.GetFullLogFilePath());
            HandleFileError?.Invoke(fileError);

            if (fileError.NewLogFileName != null)
            {
                options.LogFileName = fileError.NewLogFileName;
                _fileWriter = new FileWriter(this);
            }
            else
            {
                // If error handling didn't provide a fallback, use a default fallback
                var fallbackFileName = $"fallback_{DateTime.UtcNow:yyyyMMdd_HHmmss}.log";
                options.LogFileName = fallbackFileName;
                _fileWriter = new FileWriter(this);
            }
        }
    }

    /// <summary>
    /// Releases all resources used by the <see cref="FileLoggerProvider"/>.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;

        try
        {
            // Signal the queue to stop accepting new items
            _entryQueue.CompleteAdding();

            // Signal the background task to stop
            _cancellationTokenSource.Cancel();

            // Flush any remaining entries if possible
            if (!_entryQueue.IsCompleted && _entryQueue.Count > 0)
            {
                FlushQueue();
            }

            // Wait for the background task to complete with timeout
            if (_processQueueTask != Task.CompletedTask)
            {
                try
                {
                    // Use a reasonable timeout
                    if (_processQueueTask != null && !_processQueueTask.Wait(TimeSpan.FromSeconds(3)))
                    {
                        Debug.WriteLine("FileLoggerProvider: Timed out waiting for queue processing to complete");
                    }
                }
                catch (Exception ex) when (ex is TaskCanceledException ||
                                          (ex is AggregateException aex &&
                                           aex.InnerExceptions.Count == 1 &&
                                           aex.InnerExceptions[0] is TaskCanceledException))
                {
                    // Expected exception when task is canceled
                }
            }

            // Release resources
            _fileWriter.Dispose();
            _cancellationTokenSource.Dispose();
            _entryQueue.Dispose();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error during FileLoggerProvider disposal: {ex.Message}");
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Writes a log entry to the queue or directly to file based on configuration.
    /// </summary>
    /// <param name="message">The log message.</param>
    internal void WriteEntry(string message)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (Options.UseBackgroundThread)
        {
            // Asynchronous mode - add to queue
            if (!_entryQueue.IsAddingCompleted)
            {
                try
                {
                    if (_blockWhenQueueFull)
                    {
                        // Block until space is available
                        _entryQueue.Add(message);
                    }
                    else
                    {
                        // Try to add without blocking
                        if (!_entryQueue.TryAdd(message))
                        {
                            Interlocked.Increment(ref _entriesDroppedCount);
                        }
                    }
                }
                catch (InvalidOperationException)
                {
                    // Queue is completed or disposed
                }
            }
        }
        else
        {
            // Synchronous mode - write directly
            try
            {
                _fileWriter.WriteMessage(message, true);
                Interlocked.Increment(ref _totalEntriesWritten);
            }
            catch (Exception ex)
            {
                HandleWriteError(ex, message);
            }
        }
    }

    /// <summary>
    /// Processes the queue continuously in a background thread.
    /// </summary>
    private void ProcessQueueContinuously()
    {
        var token = _cancellationTokenSource.Token;
        var writeMessageFailed = false;
        var lastFlushTime = DateTime.UtcNow;
        var flushInterval = Options.FlushInterval;

        try
        {
            while (!token.IsCancellationRequested)
            {
                string? message = null;
                var shouldFlush = false;

                // Process entries from queue with timeout
                try
                {
                    if (_entryQueue.TryTake(out message, 100, token))
                    {
                        // Check if we should flush based on time interval
                        var now = DateTime.UtcNow;
                        shouldFlush = (now - lastFlushTime) >= flushInterval || _entryQueue.Count == 0;

                        if (shouldFlush)
                        {
                            lastFlushTime = now;
                        }

                        // Write the message if we haven't encountered a fatal error
                        if (!writeMessageFailed)
                        {
                            try
                            {
                                _fileWriter.WriteMessage(message, shouldFlush);
                                Interlocked.Increment(ref _totalEntriesWritten);
                            }
                            catch (Exception ex)
                            {
                                writeMessageFailed = !HandleWriteError(ex, message);
                            }
                        }
                    }
                    else if (_entryQueue.IsCompleted)
                    {
                        // Queue is completed, exit the loop
                        break;
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected when token is canceled
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Unexpected error in FileLoggerProvider queue processing: {ex}");
        }
    }

    /// <summary>
    /// Handles a write error by invoking the error handler if available.
    /// </summary>
    /// <param name="ex">The exception that occurred.</param>
    /// <param name="message">The message that failed to write.</param>
    /// <returns>True if the error was handled and writing can continue, false otherwise.</returns>
    private bool HandleWriteError(Exception ex, string message)
    {
        // If no error handler is configured, we can't recover
        if (HandleFileError == null)
            return false;

        try
        {
            var fileError = new FileError(ex, Options.GetFullLogFilePath());
            HandleFileError(fileError);

            if (fileError.NewLogFileName != null)
            {
                // Use the new file name provided by the error handler
                _fileWriter.UseNewLogFile(fileError.NewLogFileName);

                // Try writing the failed message to the new file
                _fileWriter.WriteMessage(message, true);
                Interlocked.Increment(ref _totalEntriesWritten);

                return true;
            }
        }
        catch (Exception handlerEx)
        {
            Debug.WriteLine($"Error in FileLoggerProvider error handler: {handlerEx}");
        }

        return false;
    }

    /// <summary>
    /// Flushes all pending log entries to disk immediately.
    /// </summary>
    public void FlushQueue()
    {
        if (_isDisposed)
            return;

        try
        {
            if (Options.UseBackgroundThread)
            {
                // Process all remaining entries in the queue
                while (_entryQueue.Count > 0 && !_entryQueue.IsCompleted)
                {
                    if (_entryQueue.TryTake(out var message))
                    {
                        try
                        {
                            _fileWriter.WriteMessage(message, true);
                            Interlocked.Increment(ref _totalEntriesWritten);
                        }
                        catch (Exception ex)
                        {
                            HandleWriteError(ex, message);
                        }
                    }
                }
            }

            // Force a final flush
            _fileWriter.Flush();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error flushing file logger queue: {ex}");
        }
    }

    /// <summary>
    /// Returns diagnostic information about this logger provider.
    /// </summary>
    public string GetDiagnosticInfo()
    {
        var uptime = DateTime.UtcNow - _startTime;

        return $"FileLoggerProvider Status [UTC: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}]" + Environment.NewLine +
               $"- Current User: {Environment.UserName}" + Environment.NewLine +
               $"- Log Files: {Path.GetFullPath(Path.Combine(Options.LogDirectory, Options.LogFileName))}" + Environment.NewLine +
               $"- Entries Written: {TotalEntriesWritten:N0}" + Environment.NewLine +
               $"- Entries Dropped: {EntriesDroppedCount:N0}" + Environment.NewLine +
               $"- Queue Size: {QueuedEntryCount:N0}/{Options.MaxQueueSize}" + Environment.NewLine +
               $"- Uptime: {uptime.TotalHours:N1} hours";
    }
}
