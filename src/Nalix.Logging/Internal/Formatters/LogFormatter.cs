// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Text;
using Nalix.Common.Diagnostics;
using Nalix.Logging.Internal.Pooling;

#if DEBUG
[assembly: InternalsVisibleTo("Nalix.Logging.Tests")]
[assembly: InternalsVisibleTo("Nalix.Logging.Benchmarks")]
#endif

namespace Nalix.Logging.Internal.Formatters;

/// <summary>
/// The Logging Formatter class provides methods for formatting log output.
/// </summary>
[DebuggerDisplay("Colors={_colors}")]
[ExcludeFromCodeCoverage]
internal class LogFormatter(bool colors = false) : ILoggerFormatter
{
    #region Fields

    private readonly bool _colors = colors;

    #endregion Fields

    #region APIs

    /// <summary>
    /// Format a log message with timestamp, log level, event ProtocolType, message and exception.
    /// </summary>
    /// <param name="logMsg">The log message to format.</param>
    /// <returns>The log format string.</returns>
    /// <example>
    /// var formatter = new NLogixFormatter();
    /// string log = formatter.FormatLog(logEntry);
    /// </example>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public string Format(LogEntry logMsg) => this.Format(logMsg.TimeStamp, logMsg.LogLevel, logMsg.EventId, logMsg.Message, logMsg.Exception);

    /// <summary>
    /// Formats a static log message.
    /// </summary>
    /// <param name="timeStamp">Time of log creation.</param>
    /// <param name="logLevel">Log level.</param>
    /// <param name="eventId">Event ProtocolType.</param>
    /// <param name="message">Log message.</param>
    /// <param name="exception">The exception included (if any).</param>
    /// <returns>Log format string.</returns>
    /// <example>
    /// string log = NLogixFormatter.FormatLogEntry(TimeStamp.UtcNow, LogLevel.Information, new EventId(1), "Sample message", null);
    /// </example>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public string Format(
        DateTime timeStamp, LogLevel logLevel,
        EventId eventId, string message, Exception? exception)
    {
        // Use pooled StringBuilder for optimal memory usage
        StringBuilder logBuilder = StringBuilderPool.Rent(capacity: 256);

        try
        {
            LogMessageBuilder.AppendFormatted(logBuilder, timeStamp, logLevel, eventId, message, exception, _colors);
            return logBuilder.ToString();
        }
        finally
        {
            StringBuilderPool.Return(logBuilder);
        }
    }

    #endregion APIs
}
