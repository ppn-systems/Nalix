// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Logging.Abstractions;
using Nalix.Common.Logging.Models;
using Nalix.Logging.Formatters;
using Nalix.Logging.Internal;

namespace Nalix.Logging.Sinks.File;

/// <summary>
/// Provides a channel-based file logging target, optimized for non-blocking log producers
/// with a batched background consumer.
/// </summary>
/// <remarks>
/// This class is plug-compatible with <see cref="ILoggerTarget"/> implementations such as
/// <see cref="FileLogTarget"/>, but it uses <see cref="ChannelFileLoggerProvider"/> internally
/// to buffer and asynchronously write logs to the file system.
/// </remarks>
public sealed class ChannelFileLogTarget : ILoggerTarget, System.IDisposable
{
    private readonly ChannelFileLoggerProvider _provider;
    private readonly ILoggerFormatter _formatter;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChannelFileLogTarget"/> class with the specified formatter and options.
    /// </summary>
    /// <param name="formatter">The log formatter used to convert log entries into string format.</param>
    /// <param name="options">The file log options to configure file paths, size limits, and rolling behavior.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="formatter"/> or <paramref name="options"/> is null.</exception>
    public ChannelFileLogTarget(ILoggerFormatter formatter, FileLogOptions options)
    {
        System.ArgumentNullException.ThrowIfNull(formatter);
        System.ArgumentNullException.ThrowIfNull(options);

        _formatter = formatter;
        _provider = new ChannelFileLoggerProvider(options);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChannelFileLogTarget"/> class with default options.
    /// </summary>
    /// <remarks>
    /// Uses the default <see cref="LoggingFormatter"/> and <see cref="FileLogOptions"/>.
    /// </remarks>
    public ChannelFileLogTarget() : this(new LoggingFormatter(false), new FileLogOptions())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChannelFileLogTarget"/> class with the specified options.
    /// </summary>
    /// <param name="options">The file log options to configure file paths, size limits, and rolling behavior.</param>
    public ChannelFileLogTarget(FileLogOptions options) : this(new LoggingFormatter(false), options)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChannelFileLogTarget"/> class with custom configuration logic.
    /// </summary>
    /// <param name="configureOptions">An action that configures the <see cref="FileLogOptions"/> before use.</param>
    public ChannelFileLogTarget(System.Action<FileLogOptions> configureOptions)
        : this(new LoggingFormatter(false), Configure(configureOptions))
    {
    }

    /// <summary>
    /// Publishes a log entry to the file logging channel.
    /// </summary>
    /// <param name="logMessage">The log entry to be written.</param>
    public void Publish(LogEntry logMessage)
        => _provider.WriteEntry(_formatter.FormatLog(logMessage));

    /// <summary>
    /// Releases all resources used by the <see cref="ChannelFileLogTarget"/>.
    /// </summary>
    /// <remarks>
    /// Ensures the log queue is flushed before disposal.
    /// </remarks>
    public void Dispose()
    {
        _provider.FlushQueue();
        _provider.Dispose();
        System.GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Applies configuration to <see cref="FileLogOptions"/> using the provided delegate.
    /// </summary>
    /// <param name="configure">The action that configures the file log options.</param>
    /// <returns>A configured <see cref="FileLogOptions"/> instance.</returns>
    private static FileLogOptions Configure(System.Action<FileLogOptions> configure)
    {
        var opts = new FileLogOptions();
        configure?.Invoke(opts);
        return opts;
    }
}
