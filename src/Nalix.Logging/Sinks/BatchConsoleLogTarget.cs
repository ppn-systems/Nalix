// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Nalix.Logging.Configuration;
using Nalix.Logging.Formatters;
using Nalix.Logging.Internal.Console;

namespace Nalix.Logging.Sinks;

/// <summary>
/// A logging target that buffers log messages and periodically writes them to the console.
/// This approach improves performance by reducing console IEndpointKey /O operations when logging frequently.
/// </summary>
[DebuggerNonUserCode]
[ExcludeFromCodeCoverage]
[DebuggerDisplay("Buffered={_count}, Max={_maxBufferSize}, Disposed={_disposed}")]
public sealed class BatchConsoleLogTarget : INLogixTarget, IDisposable
{
    #region Fields

    private readonly ConsoleLoggerProvider _provider;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the total number of log entries successfully written to the console.
    /// </summary>
    public long WrittenCount => _provider.WrittenCount;

    /// <summary>
    /// Gets the total number of log entries that were dropped and not written to the console.
    /// </summary>
    public long DroppedCount => _provider.DroppedCount;

    #endregion Properties

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchConsoleLogTarget"/> class.
    /// Optionally configures the batch console log options.
    /// </summary>
    /// <param name="options">
    /// An optional delegate to configure <see cref="ConsoleLogOptions"/> for this log target.
    /// </param>
    /// <param name="formatter"></param>
    [SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "<Pending>")]
    public BatchConsoleLogTarget(ConsoleLogOptions? options = null, INLogixFormatter? formatter = null)
        => _provider = new ConsoleLoggerProvider(formatter ?? new AnsiColorFormatter(), options);

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchFileLogTarget"/> class with custom configuration logic.
    /// </summary>
    /// <param name="options">An action that configures the <see cref="FileLogOptions"/> before use.</param>
    /// <param name="formatter"></param>
    public BatchConsoleLogTarget(Action<ConsoleLogOptions> options, INLogixFormatter? formatter = null)
        : this(Configure(options), formatter)
    {
    }

    #endregion Constructors

    #region API

    /// <inheritdoc/>
    public void Publish(
        DateTime timestampUtc,
        LogLevel logLevel,
        EventId eventId,
        string message,
        Exception? exception)
    {
        if (!_provider.TryEnqueue(timestampUtc, logLevel, eventId, message, exception))
        {
#if DEBUG
            Debug.WriteLine($"[LG.BatchConsoleLogTarget] dropped-log msg={message}");
#endif
        }
    }

    #endregion API

    #region Private Methods

    /// <summary>
    /// Configures the <see cref="ConsoleLogOptions"/> by invoking the provided action.
    /// </summary>
    /// <param name="configureOptions">The action used to configure the options.</param>
    /// <returns>The configured <see cref="ConsoleLogOptions"/>.</returns>
    [Pure]
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ConsoleLogOptions Configure(Action<ConsoleLogOptions>? configureOptions)
    {
        ConsoleLogOptions options = new();

        if (configureOptions is not null)
        {
            configureOptions(options);
        }

        return options;
    }

    #endregion Private Methods

    #region IDisposable

    /// <summary>
    /// Releases resources used by the <see cref="BatchConsoleLogTarget"/> instance.
    /// Flushes any remaining logs in the buffer before shutting down.
    /// </summary>
    [StackTraceHidden]
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Dispose() => _provider.Dispose();

    #endregion IDisposable
}
