// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Logging;
using Nalix.Logging.Core;
using Nalix.Logging.Internal.File;
using Nalix.Logging.Options;

namespace Nalix.Logging.Sinks;

/// <summary>
/// Provides a channel-based file logging target, optimized for non-blocking log producers
/// with a batched background consumer.
/// </summary>
/// <remarks>
/// This class is plug-compatible with <see cref="ILoggerTarget"/> implementations such as
/// <see cref="BatchFileLogTarget"/>, but it uses <see cref="ChannelFileLoggerProvider"/> internally
/// to buffer and asynchronously write logs to the file system.
/// </remarks>
[System.Diagnostics.DebuggerNonUserCode]
[System.Diagnostics.DebuggerDisplay("ChannelFileLogTarget")]
public sealed class BatchFileLogTarget : ILoggerTarget, System.IDisposable
{
    #region Fields

    private readonly ChannelFileLoggerProvider _provider;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchFileLogTarget"/> class with the specified formatter and options.
    /// </summary>
    /// <param name="options">The file log options to configure file paths, size limits, and rolling behavior.</param>
    /// <param name="formatter">The log formatter used to convert log entries into string format.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="formatter"/> or <paramref name="options"/> is null.</exception>
    public BatchFileLogTarget(FileLogOptions options, ILoggerFormatter formatter)
    {
        System.ArgumentNullException.ThrowIfNull(options);
        System.ArgumentNullException.ThrowIfNull(formatter);

        _provider = new ChannelFileLoggerProvider(options, formatter);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchFileLogTarget"/> class with default options.
    /// </summary>
    /// <remarks>
    /// Uses the default <see cref="NLogixFormatter"/> and <see cref="FileLogOptions"/>.
    /// </remarks>
    public BatchFileLogTarget() : this(new FileLogOptions(), new NLogixFormatter(false))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchFileLogTarget"/> class with the specified options.
    /// </summary>
    /// <param name="options">The file log options to configure file paths, size limits, and rolling behavior.</param>
    public BatchFileLogTarget(FileLogOptions options) : this(options, new NLogixFormatter(false))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchFileLogTarget"/> class with custom configuration logic.
    /// </summary>
    /// <param name="options">An action that configures the <see cref="FileLogOptions"/> before use.</param>
    public BatchFileLogTarget(System.Action<FileLogOptions> options)
        : this(Configure(options), new NLogixFormatter(false))
    {
    }

    #endregion Constructors

    #region APIs

    /// <summary>
    /// Publishes a log entry to the file logging channel.
    /// </summary>
    /// <param name="entry">The log entry to be written.</param>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Publish(LogEntry entry) => _provider.Enqueue(entry);

    /// <summary>
    /// Releases all resources used by the <see cref="BatchFileLogTarget"/>.
    /// </summary>
    /// <remarks>
    /// Ensures the log queue is flushed before disposal.
    /// </remarks>
    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void Dispose()
    {
        _provider.Flush();
        _provider.Dispose();
        System.GC.SuppressFinalize(this);
    }

    #endregion APIs

    #region Private Methods

    /// <summary>
    /// Applies configuration to <see cref="FileLogOptions"/> using the provided delegate.
    /// </summary>
    /// <param name="configure">The action that configures the file log options.</param>
    /// <returns>A configured <see cref="FileLogOptions"/> instance.</returns>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static FileLogOptions Configure(System.Action<FileLogOptions> configure)
    {
        FileLogOptions opts = new();
        configure?.Invoke(opts);
        return opts;
    }

    #endregion Private Methods
}
