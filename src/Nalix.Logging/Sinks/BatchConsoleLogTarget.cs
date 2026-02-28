// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Diagnostics;
using Nalix.Logging.Configuration;
using Nalix.Logging.Internal.Console;

namespace Nalix.Logging.Sinks;

/// <summary>
/// A logging target that buffers log messages and periodically writes them to the console.
/// This approach improves performance by reducing console IEndpointKey /O operations when logging frequently.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[System.Diagnostics.DebuggerDisplay("Buffered={_count}, Max={_maxBufferSize}, Disposed={_disposed}")]
public sealed class BatchConsoleLogTarget : ILoggerTarget, System.IDisposable
{
    #region Fields

    private readonly ConsoleLoggerProvider _provider;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the total number of log entries successfully written to the console.
    /// </summary>
    public System.Int64 WrittenCount => _provider.WrittenCount;

    /// <summary>
    /// Gets the total number of log entries that were dropped and not written to the console.
    /// </summary>
    public System.Int64 DroppedCount => _provider.DroppedCount;

    #endregion Properties

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchConsoleLogTarget"/> class.
    /// Optionally configures the batch console log options.
    /// </summary>
    /// <param name="options">
    /// An optional delegate to configure <see cref="ConsoleLogOptions"/> for this log target.
    /// </param>
    public BatchConsoleLogTarget(ConsoleLogOptions? options = null)
    {
        System.Console.Title = "Nx";
        _provider = new ConsoleLoggerProvider(options);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchFileLogTarget"/> class with custom configuration logic.
    /// </summary>
    /// <param name="options">An action that configures the <see cref="FileLogOptions"/> before use.</param>
    public BatchConsoleLogTarget(System.Action<ConsoleLogOptions> options)
        : this(Configure(options))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchConsoleLogTarget"/> class with default options.
    /// </summary>
    public BatchConsoleLogTarget()
    {
        System.Console.Title = "Nx";
        _provider = new ConsoleLoggerProvider(null);
    }

    #endregion Constructors

    #region API

    /// <inheritdoc/>
    public void Publish(LogEntry entry)
    {
        if (!_provider.TryEnqueue(entry))
        {
            System.Diagnostics.Debug.WriteLine($"[LG.BatchConsoleLogTarget] dropped-log msg={entry.Message}");
        }
    }

    /// <summary>
    /// Asynchronously writes a single <see cref="LogEntry"/> to the console log buffer.
    /// The entry will be batched and written to the console according to the configured batch options.
    /// </summary>
    /// <param name="entry">The log entry to write.</param>
    /// <returns>A <see cref="System.Threading.Tasks.ValueTask"/> representing the asynchronous write operation.</returns>
    public System.Threading.Tasks.ValueTask WriteAsync(LogEntry entry) => _provider.WriteAsync(entry);

    #endregion API

    #region Private Methods

    /// <summary>
    /// Configures the <see cref="ConsoleLogOptions"/> by invoking the provided action.
    /// </summary>
    /// <param name="configureOptions">The action used to configure the options.</param>
    /// <returns>The configured <see cref="ConsoleLogOptions"/>.</returns>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static ConsoleLogOptions Configure(System.Action<ConsoleLogOptions> configureOptions)
    {
        ConsoleLogOptions options = new();
        configureOptions(options);
        return options;
    }

    #endregion Private Methods

    #region IDisposable

    /// <summary>
    /// Releases resources used by the <see cref="BatchConsoleLogTarget"/> instance.
    /// Flushes any remaining logs in the buffer before shutting down.
    /// </summary>
    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void Dispose() => _provider.Dispose();

    #endregion IDisposable
}
