// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Nalix.Common.Diagnostics;
using Nalix.Logging.Configuration;
using Nalix.Logging.Formatters;
using Nalix.Logging.Internal.File;

namespace Nalix.Logging.Sinks;

/// <summary>
/// Provides a channel-based file logging target, optimized for non-blocking log producers
/// with a batched background consumer.
/// </summary>
/// <remarks>
/// This class is plug-compatible with <see cref="ILoggerTarget"/> implementations such as
/// <see cref="BatchFileLogTarget"/>, but it uses <see cref="FileLoggerProvider"/> internally
/// to buffer and asynchronously write logs to the file system.
/// </remarks>
[DebuggerNonUserCode]
[DebuggerDisplay("ChannelFileLogTarget")]
public sealed class BatchFileLogTarget : ILoggerTarget, IDisposable
{
    #region Fields

    private readonly FileLoggerProvider _provider;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchFileLogTarget"/> class with the specified formatter and options.
    /// </summary>
    /// <param name="options">The file log options to configure file paths, size limits, and rolling behavior.</param>
    /// <param name="formatter">The log formatter used to convert log entries into string format.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="formatter"/> or <paramref name="options"/> is null.</exception>
    public BatchFileLogTarget(FileLogOptions? options, ILoggerFormatter formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);

        _provider = new FileLoggerProvider(formatter, options);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchFileLogTarget"/> class with default options.
    /// </summary>
    /// <remarks>
    /// Uses the default <see cref="AnsiColorFormatter"/> and <see cref="FileLogOptions"/>.
    /// </remarks>
    public BatchFileLogTarget() : this(null, new FileLogFormatter())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchFileLogTarget"/> class with custom configuration logic.
    /// </summary>
    /// <param name="options">An action that configures the <see cref="FileLogOptions"/> before use.</param>
    public BatchFileLogTarget(Action<FileLogOptions> options)
        : this(Configure(options), new FileLogFormatter())
    {
    }

    #endregion Constructors

    #region APIs

    /// <summary>
    /// Publishes a log entry to the file logging channel.
    /// </summary>
    /// <param name="logMessage">The log entry to be written.</param>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Publish(LogEntry logMessage) => _provider.Enqueue(logMessage);

    /// <summary>
    /// Releases all resources used by the <see cref="BatchFileLogTarget"/>.
    /// </summary>
    /// <remarks>
    /// Ensures the log queue is flushed before disposal.
    /// </remarks>
    [StackTraceHidden]
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Dispose()
    {
        _provider.Flush();
        _provider.Dispose();
        GC.SuppressFinalize(this);
    }

    #endregion APIs

    #region Private Methods

    /// <summary>
    /// Applies configuration to <see cref="FileLogOptions"/> using the provided delegate.
    /// </summary>
    /// <param name="configure">The action that configures the file log options.</param>
    /// <returns>A configured <see cref="FileLogOptions"/> instance.</returns>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static FileLogOptions Configure(Action<FileLogOptions> configure)
    {
        FileLogOptions opts = new();
        configure?.Invoke(opts);
        return opts;
    }

    #endregion Private Methods
}
