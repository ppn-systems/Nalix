// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Diagnostics;
using Nalix.Logging.Configuration;
using Nalix.Logging.Internal.Webhook;

namespace Nalix.Logging.Sinks;

/// <summary>
/// A logging target that buffers log messages and sends them to Discord webhook in batches.
/// This approach improves performance by reducing HTTP requests when logging frequently.
/// </summary>
/// <remarks>
/// Uses channel-based batching to queue logs and send them asynchronously to Discord.
/// Supports retry logic, rate limiting, and Discord embed formatting.
/// </remarks>
[System.Diagnostics.DebuggerNonUserCode]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[System.Diagnostics.DebuggerDisplay("Buffered={WrittenCount}, Dropped={DroppedCount}, Disposed={_disposed}")]
public sealed class BatchWebhookLogTarget : ILoggerTarget, System.IDisposable
{
    #region Fields

    private readonly WebhookLoggerProvider _provider;
    private volatile System.Boolean _disposed;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the total number of log entries successfully sent to Discord.
    /// </summary>
    public System.Int64 WrittenCount => _provider.WrittenCount;

    /// <summary>
    /// Gets the total number of log entries that were dropped and not sent to Discord.
    /// </summary>
    public System.Int64 DroppedCount => _provider.DroppedCount;

    /// <summary>
    /// Gets the total number of failed webhook calls after all retry attempts.
    /// </summary>
    public System.Int64 FailedCount => _provider.FailedCount;

    #endregion Properties

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchWebhookLogTarget"/> class with specified options.
    /// </summary>
    /// <param name="options">The webhook log options to configure Discord webhook settings.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
    public BatchWebhookLogTarget(WebhookLogOptions? options = null)
    {
        System.ArgumentNullException.ThrowIfNull(options);
        _provider = new WebhookLoggerProvider(options);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchWebhookLogTarget"/> class with custom configuration logic.
    /// </summary>
    /// <param name="configureOptions">An action that configures the <see cref="WebhookLogOptions"/> before use.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="configureOptions"/> is null.</exception>
    public BatchWebhookLogTarget(System.Action<WebhookLogOptions> configureOptions)
        : this(Configure(configureOptions))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchWebhookLogTarget"/> class with custom configuration logic.
    /// </summary>
    public BatchWebhookLogTarget() => _provider = new WebhookLoggerProvider();

    #endregion Constructors

    #region API

    /// <inheritdoc/>
    /// <remarks>
    /// Attempts to enqueue the log entry to the channel.
    /// If the queue is full and <see cref="WebhookLogOptions.BlockWhenQueueFull"/> is false,
    /// the entry will be dropped.
    /// </remarks>
    public void Publish(LogEntry entry)
    {
        if (_disposed)
        {
            return;
        }

        // Filter by minimum level
        if (entry.LogLevel < _provider.MinimumLevel)
        {
            return;
        }

        if (!_provider.TryEnqueue(entry))
        {
            System.Diagnostics.Debug.WriteLine(
                $"[LG.BatchWebhookLogTarget] dropped-log level={entry.LogLevel} msg={entry.Message}");
        }
    }

    /// <summary>
    /// Asynchronously writes a single <see cref="LogEntry"/> to the webhook log buffer.
    /// The entry will be batched and sent to Discord according to the configured batch options.
    /// </summary>
    /// <param name="entry">The log entry to write.</param>
    /// <returns>A <see cref="System.Threading. Tasks.ValueTask"/> representing the asynchronous write operation.</returns>
    public System.Threading.Tasks.ValueTask WriteAsync(LogEntry entry)
    {
        return _disposed || entry.LogLevel < _provider.MinimumLevel
            ? System.Threading.Tasks.ValueTask.CompletedTask
            : _provider.WriteAsync(entry);
    }

    #endregion API

    #region Private Methods

    /// <summary>
    /// Configures the <see cref="WebhookLogOptions"/> by invoking the provided action.
    /// </summary>
    /// <param name="configureOptions">The action used to configure the options.</param>
    /// <returns>The configured <see cref="WebhookLogOptions"/>.</returns>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static WebhookLogOptions Configure(System.Action<WebhookLogOptions> configureOptions)
    {
        System.ArgumentNullException.ThrowIfNull(configureOptions);

        WebhookLogOptions options = new();
        configureOptions(options);
        return options;
    }

    #endregion Private Methods

    #region IDisposable

    /// <summary>
    /// Releases resources used by the <see cref="BatchWebhookLogTarget"/> instance.
    /// Flushes any remaining logs in the buffer before shutting down.
    /// </summary>
    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _provider.Dispose();
        System.GC.SuppressFinalize(this);
    }

    #endregion IDisposable
}