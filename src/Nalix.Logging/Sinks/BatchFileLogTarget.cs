// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Nalix.Logging.Formatters;
using Nalix.Logging.Internal.File;
using Nalix.Logging.Options;

namespace Nalix.Logging.Sinks;

/// <summary>
/// A buffered logging target that writes log entries to a file.
/// </summary>
/// <remarks>
/// This target buffers log entries and flushes them through <see cref="FileLoggerProvider"/>.
/// </remarks>
[DebuggerNonUserCode]
[DebuggerDisplay("ChannelFileLogTarget")]
public sealed class BatchFileLogTarget : INLogixTarget, IDisposable
{
    #region Fields

    private readonly FileLoggerProvider _provider;

    #endregion Fields

    #region Constructors

    /// <summary>Initializes a new instance of the <see cref="BatchFileLogTarget"/> class.</summary>
    /// <param name="options">The file log options to configure file paths, size limits, and rolling behavior.</param>
    /// <param name="formatter">The log formatter used to convert log entries into string format.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="formatter"/> or <paramref name="options"/> is null.</exception>
    public BatchFileLogTarget(FileLogOptions? options, INLogixFormatter formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);

        _provider = new FileLoggerProvider(formatter, options);
    }

    /// <summary>Initializes a new instance of the <see cref="BatchFileLogTarget"/> class with default options.</summary>
    public BatchFileLogTarget() : this(null, new FileLogFormatter())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="BatchFileLogTarget"/> class with custom configuration logic.</summary>
    /// <param name="options">An action that configures the <see cref="FileLogOptions"/> before use.</param>
    public BatchFileLogTarget(Action<FileLogOptions> options)
        : this(Configure(options), new FileLogFormatter())
    {
    }

    #endregion Constructors

    #region APIs

    /// <inheritdoc/>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Publish(
        DateTime timestampUtc,
        LogLevel logLevel,
        EventId eventId,
        string message,
        Exception? exception)
        => _provider.Enqueue(timestampUtc, logLevel, eventId, message, exception);

    /// <inheritdoc/>
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

    /// <summary>Applies configuration to <see cref="FileLogOptions"/> using the provided delegate.</summary>
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
