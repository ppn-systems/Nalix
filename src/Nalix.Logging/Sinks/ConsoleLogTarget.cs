// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Logging;
using Nalix.Logging.Core;
using System.Threading.Channels;

namespace Nalix.Logging.Sinks;

/// <summary>
/// Non-blocking console logging target that uses System.Threading.Channels for async processing.
/// This implementation ensures that console I/O never blocks the calling thread, making it suitable
/// for high-throughput server applications like TCP servers with many concurrent connections.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[System.Diagnostics.DebuggerDisplay("ConsoleTarget Colors={_formatter != null}, Pending={_channel?.Reader.Count ?? 0}")]
public sealed class ConsoleLogTarget : ILoggerTarget, System.IDisposable
{
    #region Fields

    private readonly ILoggerFormatter _loggerFormatter;
    private readonly Channel<LogEntry> _channel;
    private readonly System.Threading.CancellationTokenSource _cts;
    private readonly System.Threading.Tasks.Task _processingTask;
    private volatile System.Boolean _disposed;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsoleLogTarget"/> class with a default log formatter.
    /// </summary>
    /// <param name="loggerFormatter">The object responsible for formatting the log message.</param>
    public ConsoleLogTarget(ILoggerFormatter loggerFormatter)
    {
        System.ArgumentNullException.ThrowIfNull(loggerFormatter);
        System.Console.Title = "Logging";

        _loggerFormatter = loggerFormatter;

        // Create unbounded channel for non-blocking writes
        _channel = Channel.CreateUnbounded<LogEntry>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });

        _cts = new System.Threading.CancellationTokenSource();
        _processingTask = System.Threading.Tasks.Task.Run(() => ProcessLogEntriesAsync(_cts.Token));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsoleLogTarget"/> class with a default log formatter.
    /// </summary>
    public ConsoleLogTarget() : this(new NLogixFormatter(true))
    {
    }

    #endregion Constructors

    #region Public Methods

    /// <summary>
    /// Publishes a log message to the console asynchronously without blocking.
    /// This is a lock-free, non-blocking operation suitable for high-throughput scenarios.
    /// </summary>
    /// <param name="logMessage">The log message to be outputted.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Publish(LogEntry logMessage)
    {
        if (_disposed)
        {
            return;
        }

        // TryWrite is lock-free and non-blocking for unbounded channels
        _ = _channel.Writer.TryWrite(logMessage);
    }

    #endregion Public Methods

    #region Private Methods

    /// <summary>
    /// Background task that processes log entries from the channel.
    /// </summary>
    private async System.Threading.Tasks.Task ProcessLogEntriesAsync(System.Threading.CancellationToken cancellationToken)
    {
        var reader = _channel.Reader;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Wait for items to be available
                if (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    // Process all available items
                    while (reader.TryRead(out LogEntry entry))
                    {
                        try
                        {
                            System.String formatted = _loggerFormatter.Format(entry);
                            System.Console.WriteLine(formatted);
                        }
                        catch (System.Exception ex)
                        {
                            // Swallow exceptions to prevent logging from crashing the app
                            System.Diagnostics.Debug.WriteLine($"Console write error: {ex.GetType().Name}: {ex.Message}");
                        }
                    }
                }
            }
        }
        catch (System.OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Console processing error: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            // Final flush on shutdown
            while (reader.TryRead(out LogEntry entry))
            {
                try
                {
                    System.String formatted = _loggerFormatter.Format(entry);
                    System.Console.WriteLine(formatted);
                }
                catch
                {
                    // Ignore errors during shutdown
                }
            }
        }
    }

    #endregion Private Methods

    #region IDisposable

    /// <summary>
    /// Disposes the console logger and flushes any remaining log entries.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Complete the channel to signal no more writes
        _channel.Writer.Complete();

        // Cancel the processing task
        _cts.Cancel();

        // Wait for processing to complete with timeout
        try
        {
            System.Boolean completed = _processingTask.Wait(System.TimeSpan.FromSeconds(5));
            if (!completed)
            {
                System.Diagnostics.Debug.WriteLine("ConsoleLogTarget: Processing task did not complete within timeout");
            }
        }
        catch (System.OperationCanceledException)
        {
            // Expected
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ConsoleLogTarget disposal error: {ex.GetType().Name}: {ex.Message}");
        }

        _cts.Dispose();
    }

    #endregion IDisposable
}
