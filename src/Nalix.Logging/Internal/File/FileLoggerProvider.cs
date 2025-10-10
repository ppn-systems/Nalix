// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Environment;
using Nalix.Logging.Internal.Exceptions;
using Nalix.Logging.Options;

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Logging.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Logging.Benchmarks")]
#endif

namespace Nalix.Logging.Internal.File;

/// <summary>
/// A high-performance provider for file-based logging with support for file rotation and error handling.
/// </summary>
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[System.Diagnostics.DebuggerDisplay("Queued={QueuedEntryCount}, Written={TotalEntriesWritten}, Dropped={EntriesDroppedCount}")]
internal sealed class FileLoggerProvider : System.IDisposable
{
    #region Fields

    private readonly FileWriter _fileWriter;
    private readonly System.Int32 _maxQueueSize;
    private readonly System.Boolean _blockWhenQueueFull;
    private readonly System.Threading.Tasks.Task? _processQueueTask;
    private readonly System.DateTime _startTime = System.DateTime.UtcNow;
    private readonly System.Threading.CancellationTokenSource _cancellationTokenSource = new();
    private readonly System.Collections.Concurrent.BlockingCollection<System.String> _entryQueue;

    private System.Boolean _isDisposed;

    // Stats for monitoring
    private System.Int64 _totalEntriesWritten;

    private System.Int64 _entriesDroppedCount;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the configuration options used by this logger provider.
    /// </summary>
    public FileLogOptions Options { get; }

    /// <summary>
    /// Gets a value indicating whether the log file will be appended to (true) or overwritten (false).
    /// </summary>
    public System.Boolean Append => Options.Append;

    /// <summary>
    /// Gets the maximum size of the log file in bytes before it rolls over.
    /// </summary>
    public System.Int32 MaxFileSize => Options.MaxFileSizeBytes;

    /// <summary>
    /// Custom formatter for log file names.
    /// </summary>
    public System.Func<System.String, System.String>? FormatLogFileName
    {
        get => Options.FormatLogFileName;
        set => Options.FormatLogFileName = value;
    }

    /// <summary>
    /// Custom handler for file errors.
    /// </summary>
    public System.Action<FileError>? HandleFileError
    {
        get => Options.HandleFileError;
        set => Options.HandleFileError = value;
    }

    /// <summary>
    /// Gets the ProtocolType of entries currently in the queue waiting to be written.
    /// </summary>
    public System.Int32 QueuedEntryCount => _entryQueue.Count;

    /// <summary>
    /// Gets the total ProtocolType of log entries written since this provider was created.
    /// </summary>
    public System.Int64 TotalEntriesWritten => System.Threading.Interlocked.Read(ref _totalEntriesWritten);

    /// <summary>
    /// Gets the ProtocolType of entries that were dropped due to queue capacity constraints.
    /// </summary>
    public System.Int64 EntriesDroppedCount => System.Threading.Interlocked.Read(ref _entriesDroppedCount);

    #endregion Properties

    #region Constructor

    /// <summary>
    /// Initializes a new instance of <see cref="FileLoggerProvider"/> with the specified configuration options.
    /// </summary>
    /// <param name="options">The configuration options.</param>
    public FileLoggerProvider(FileLogOptions options)
    {
        Options = options ?? throw new System.ArgumentNullException(nameof(options));
        _maxQueueSize = options.MaxQueueSize;
        _blockWhenQueueFull = options.BlockWhenQueueFull;
        _entryQueue = new System.Collections.Concurrent.BlockingCollection<System.String>(
            new System.Collections.Concurrent.ConcurrentQueue<System.String>(), _maxQueueSize);

        try
        {
            _fileWriter = new FileWriter(this);

            if (options.UseBackgroundThread)
            {
                _processQueueTask = System.Threading.Tasks.Task.Factory.StartNew(
                    ProcessQueueContinuously,
                    _cancellationTokenSource.Token,
                    System.Threading.Tasks.TaskCreationOptions.LongRunning,
                    System.Threading.Tasks.TaskScheduler.Default);
            }
            else
            {
                // Create a completed task for non-background mode
                _processQueueTask = System.Threading.Tasks.Task.CompletedTask;
            }
        }
        catch (System.Exception ex)
        {
            // Handle initialization errors
            FileError fileError = new(ex, options.GetFullLogFilePath());
            HandleFileError?.Invoke(fileError);

            if (fileError.NewLogFileName != null)
            {
                options.LogFileName = fileError.NewLogFileName;
                _fileWriter = new FileWriter(this);
            }
            else
            {
                // If error handling didn't provide a fallback, use a default fallback
                System.String fallbackFileName = $"fallback_{System.DateTime.UtcNow:yyyyMMdd_HHmmss}.log";
                options.LogFileName = fallbackFileName;
                _fileWriter = new FileWriter(this);
            }
        }
    }

    #endregion Constructor

    #region APIs

    /// <summary>
    /// Releases all resources used by the <see cref="FileLoggerProvider"/>.
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
            if (_processQueueTask != System.Threading.Tasks.Task.CompletedTask)
            {
                try
                {
                    // Use a reasonable timeout
                    if (_processQueueTask != null && !_processQueueTask.Wait(System.TimeSpan.FromSeconds(3)))
                    {
                        System.Diagnostics.Debug.WriteLine(
                            "FileLoggerProvider: Timed out waiting for queue processing to complete");
                    }
                }
                catch (System.Exception ex) when (ex is System.Threading.Tasks.TaskCanceledException ||
                                                 ex is System.AggregateException aex &&
                                                  aex.InnerExceptions.Count == 1 &&
                                                  aex.InnerExceptions[0] is System.Threading.Tasks.TaskCanceledException)
                {
                    // Expected exception when task is canceled
                }
            }

            // Release resources
            _fileWriter.Dispose();
            _cancellationTokenSource.Dispose();
            _entryQueue.Dispose();
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"ERROR during FileLoggerProvider disposal: {ex.Message}");
        }

        System.GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Writes a log entry to the queue or directly to file based on configuration.
    /// </summary>
    /// <param name="message">The log message.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal void WriteEntry(System.String message)
    {
        if (_isDisposed || System.String.IsNullOrEmpty(message))
        {
            return;
        }

        if (Options.UseBackgroundThread)
        {
            try
            {
                if (_blockWhenQueueFull)
                {
                    _entryQueue.Add(message);
                }
                else if (!_entryQueue.TryAdd(message))
                {
                    _ = System.Threading.Interlocked.Increment(ref _entriesDroppedCount);
                }
            }
            catch
            {
                // queue closed or canceled: swallow
                _ = System.Threading.Interlocked.Increment(ref _entriesDroppedCount);
            }
        }
        else
        {
            try
            {
                _fileWriter.WriteMessage(message, true);
                _ = System.Threading.Interlocked.Increment(ref _totalEntriesWritten);
            }
            catch (System.Exception ex)
            {
                _ = HandleWriteError(ex, message);
            }
        }
    }

    /// <summary>
    /// Flushes all pending log entries to disk immediately.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void FlushQueue()
    {
        if (_isDisposed)
        {
            return;
        }

        try
        {
            if (Options.UseBackgroundThread)
            {
                // Process all remaining entries in the queue
                while (_entryQueue.Count > 0 && !_entryQueue.IsCompleted)
                {
                    if (_entryQueue.TryTake(out System.String? message))
                    {
                        try
                        {
                            _fileWriter.WriteMessage(message, true);
                            _ = System.Threading.Interlocked.Increment(ref _totalEntriesWritten);
                        }
                        catch (System.Exception ex)
                        {
                            _ = HandleWriteError(ex, message);
                        }
                    }
                }
            }

            // Force a final flush
            _fileWriter.Flush();
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"ERROR flushing file logger queue: {ex}");
        }
    }

    /// <summary>
    /// Returns diagnostic information about this logger provider.
    /// </summary>
    [System.Diagnostics.Contracts.Pure]
    public System.String GetDiagnosticInfo()
    {
        var uptime = System.DateTime.UtcNow - _startTime;

        return $"FileLoggerProvider Status [UTC: {System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}]" +
                System.Environment.NewLine +
               $"- Current User: {System.Environment.UserName}" +
                System.Environment.NewLine +
               $"- Log Files: {System.IO.Path.GetFullPath(System.IO.Path.Combine(Directories.LogsDirectory, Options.LogFileName))}" +
                System.Environment.NewLine +
               $"- Entries Written: {TotalEntriesWritten:N0}" +
                System.Environment.NewLine +
               $"- Entries Dropped: {EntriesDroppedCount:N0}" +
                System.Environment.NewLine +
               $"- Queue Size: {QueuedEntryCount:N0}/{Options.MaxQueueSize}" +
                System.Environment.NewLine +
               $"- Uptime: {uptime.TotalHours:N1} hours";
    }

    #endregion APIs

    #region Private Methods

    /// <summary>
    /// Processes the queue continuously in a background thread.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private void ProcessQueueContinuously()
    {
        System.Threading.CancellationToken token = _cancellationTokenSource.Token;
        System.Boolean writeMessageFailed = false;
        System.DateTime lastFlushTime = System.DateTime.UtcNow;
        System.TimeSpan flushInterval = Options.FlushInterval;

        try
        {
            while (!token.IsCancellationRequested)
            {
                System.String? message = null;
                System.Boolean shouldFlush = false;

                // Process entries from queue with timeout
                try
                {
                    if (_entryQueue.TryTake(out message, 100, token))
                    {
                        // Check if we should flush based on time interval
                        System.DateTime now = System.DateTime.UtcNow;
                        shouldFlush = now - lastFlushTime >= flushInterval || _entryQueue.Count == 0;

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
                                _ = System.Threading.Interlocked.Increment(ref _totalEntriesWritten);
                            }
                            catch (System.Exception ex)
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
                catch (System.OperationCanceledException)
                {
                    // Expected when token is canceled
                    break;
                }
            }
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Unexpected error in FileLoggerProvider queue processing: {ex}");
        }
    }

    /// <summary>
    /// Handles a write error by invoking the error handler if available.
    /// </summary>
    /// <param name="ex">The exception that occurred.</param>
    /// <param name="message">The message that failed to write.</param>
    /// <returns>True if the error was handled and writing can continue, false otherwise.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private System.Boolean HandleWriteError(System.Exception ex, System.String message)
    {
        // If no error handler is configured, we can't recover
        if (HandleFileError == null)
        {
            return false;
        }

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
                _ = System.Threading.Interlocked.Increment(ref _totalEntriesWritten);

                return true;
            }
        }
        catch (System.Exception handlerEx)
        {
            System.Diagnostics.Debug.WriteLine(
                $"ERROR in FileLoggerProvider error handler: {handlerEx}");
        }

        return false;
    }

    #endregion Private Methods
}
