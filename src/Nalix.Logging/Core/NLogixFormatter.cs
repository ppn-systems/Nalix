// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Logging;
using Nalix.Logging.Internal.Formatters;

namespace Nalix.Logging.Core;

/// <summary>
/// The Logging Formatter class provides methods for formatting log output.
/// </summary>
[System.Diagnostics.DebuggerDisplay("Colors={_colors}")]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public class NLogixFormatter(System.Boolean colors = false) : ILoggerFormatter
{
    private readonly System.Boolean _colors = colors;

    /// <summary>
    /// Format a log message with timestamp, log level, event ProtocolType, message and exception.
    /// </summary>
    /// <param name="logMsg">The log message to format.</param>
    /// <returns>The log format string.</returns>
    /// <example>
    /// var formatter = new NLogixFormatter();
    /// string log = formatter.FormatLog(logEntry);
    /// </example>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.String Format(LogEntry logMsg) => Format(logMsg.TimeStamp, logMsg.LogLevel, logMsg.EventId, logMsg.Message, logMsg.Exception);

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
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.String Format(
        System.DateTime timeStamp, LogLevel logLevel,
        EventId eventId, System.String message, System.Exception? exception)
    {
        const int DefaultCapacity = 256;
        System.Text.StringBuilder logBuilder = new(DefaultCapacity);

        LogMessageBuilder.AppendFormatted(logBuilder, timeStamp, logLevel, eventId, message, exception, _colors);

        return logBuilder.ToString();
    }
}
