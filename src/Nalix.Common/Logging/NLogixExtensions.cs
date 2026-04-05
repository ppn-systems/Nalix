// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Globalization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.Logging;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Provides compact compatibility extensions for <see cref="ILogger"/>.
/// </summary>
/// <remarks>
/// These helpers preserve existing Nalix call sites while routing through Microsoft logging APIs.
/// They check <see cref="ILogger.IsEnabled(LogLevel)"/> before formatting and use a constant message
/// template to keep analyzer output clean and allocations low on hot paths.
/// </remarks>
public static class NLogixExtensions
{
    private static readonly Func<MessageLogState, Exception?, string> s_messageFormatter = static (state, _) => state.Message;

    /// <summary>
    /// Logs a trace-level message.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="message">The rendered message to write.</param>
    public static void Trace(this ILogger logger, string message)
        => LogMessage(logger, LogLevel.Trace, default, message, null);

    /// <summary>
    /// Logs a trace-level message built from a composite format string.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="format">The composite format string.</param>
    /// <param name="args">The values used to format the message.</param>
    public static void Trace(this ILogger logger, string format, params object[] args)
        => LogFormattedMessage(logger, LogLevel.Trace, default, format, args);

    /// <summary>
    /// Logs a trace-level message with an optional <see cref="EventId"/>.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="message">The rendered message to write.</param>
    /// <param name="eventId">The optional event identifier.</param>
    public static void Trace(this ILogger logger, string message, EventId? eventId = null)
        => LogMessage(logger, LogLevel.Trace, eventId, message, null);

    /// <summary>
    /// Logs a debug-level message.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="message">The rendered message to write.</param>
    public static void Debug(this ILogger logger, string message)
        => LogMessage(logger, LogLevel.Debug, default, message, null);

    /// <summary>
    /// Logs a debug-level message built from a composite format string.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="format">The composite format string.</param>
    /// <param name="args">The values used to format the message.</param>
    public static void Debug(this ILogger logger, string format, params object[] args)
        => LogFormattedMessage(logger, LogLevel.Debug, default, format, args);

    /// <summary>
    /// Logs a debug-level message with an optional <see cref="EventId"/>.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="message">The rendered message to write.</param>
    /// <param name="eventId">The optional event identifier.</param>
    public static void Debug(this ILogger logger, string message, EventId? eventId = null)
        => LogMessage(logger, LogLevel.Debug, eventId, message, null);

    /// <summary>
    /// Logs an information-level message.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="message">The rendered message to write.</param>
    public static void Info(this ILogger logger, string message)
        => LogMessage(logger, LogLevel.Information, default, message, null);

    /// <summary>
    /// Logs an information-level message built from a composite format string.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="format">The composite format string.</param>
    /// <param name="args">The values used to format the message.</param>
    public static void Info(this ILogger logger, string format, params object[] args)
        => LogFormattedMessage(logger, LogLevel.Information, default, format, args);

    /// <summary>
    /// Logs an information-level message with an optional <see cref="EventId"/>.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="message">The rendered message to write.</param>
    /// <param name="eventId">The optional event identifier.</param>
    public static void Info(this ILogger logger, string message, EventId? eventId = null)
        => LogMessage(logger, LogLevel.Information, eventId, message, null);

    /// <summary>
    /// Logs a warning-level message.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="message">The rendered message to write.</param>
    public static void Warn(this ILogger logger, string message)
        => LogMessage(logger, LogLevel.Warning, default, message, null);

    /// <summary>
    /// Logs a warning-level message built from a composite format string.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="format">The composite format string.</param>
    /// <param name="args">The values used to format the message.</param>
    public static void Warn(this ILogger logger, string format, params object[] args)
        => LogFormattedMessage(logger, LogLevel.Warning, default, format, args);

    /// <summary>
    /// Logs a warning-level message with an optional <see cref="EventId"/>.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="message">The rendered message to write.</param>
    /// <param name="eventId">The optional event identifier.</param>
    public static void Warn(this ILogger logger, string message, EventId? eventId = null)
        => LogMessage(logger, LogLevel.Warning, eventId, message, null);

    /// <summary>
    /// Logs an error-level message.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="message">The rendered message to write.</param>
    /// <param name="eventId">The optional event identifier.</param>
    public static void Error(this ILogger logger, string message, EventId? eventId = null)
        => LogMessage(logger, LogLevel.Error, eventId, message, null);

    /// <summary>
    /// Logs an error-level message built from a composite format string.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="format">The composite format string.</param>
    /// <param name="args">The values used to format the message.</param>
    public static void Error(this ILogger logger, string format, params object[] args)
        => LogFormattedMessage(logger, LogLevel.Error, default, format, args);

    /// <summary>
    /// Logs an error-level message built from a composite format string.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="exception">The associated exception.</param>
    /// <param name="format">The composite format string.</param>
    /// <param name="args">The values used to format the message.</param>
    public static void Error(this ILogger logger, Exception exception, string format, params object[] args)
        => LogFormattedMessage(logger, LogLevel.Error, default, format, args, exception);

    /// <summary>
    /// Logs an error-level message with an exception.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="message">The rendered message to write.</param>
    /// <param name="exception">The associated exception.</param>
    /// <param name="eventId">The optional event identifier.</param>
    public static void Error(this ILogger logger, string message, Exception exception, EventId? eventId = null)
        => LogMessage(logger, LogLevel.Error, eventId, message, exception);

    /// <summary>
    /// Logs a critical-level message built from a composite format string.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="format">The composite format string.</param>
    /// <param name="args">The values used to format the message.</param>
    public static void Critical(this ILogger logger, string format, params object[] args)
        => LogFormattedMessage(logger, LogLevel.Critical, default, format, args);

    /// <summary>
    /// Logs an error-level message built from a composite format string.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="exception">The associated exception.</param>
    /// <param name="format">The composite format string.</param>
    /// <param name="args">The values used to format the message.</param>
    public static void Critical(this ILogger logger, Exception exception, string format, params object[] args)
        => LogFormattedMessage(logger, LogLevel.Critical, default, format, args, exception);

    /// <summary>
    /// Logs a critical-level message with an optional <see cref="EventId"/>.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="message">The rendered message to write.</param>
    /// <param name="eventId">The optional event identifier.</param>
    public static void Critical(this ILogger logger, string message, EventId? eventId = null)
        => LogMessage(logger, LogLevel.Critical, eventId, message, null);

    /// <summary>
    /// Logs a critical-level message with an exception.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="message">The rendered message to write.</param>
    /// <param name="exception">The associated exception.</param>
    /// <param name="eventId">The optional event identifier.</param>
    public static void Critical(this ILogger logger, string message, Exception exception, EventId? eventId = null)
        => LogMessage(logger, LogLevel.Critical, eventId, message, exception);

    private static void LogFormattedMessage(ILogger logger, LogLevel level, EventId? eventId, string format, object[] args, Exception? exception = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(args);

        if (!logger.IsEnabled(level))
        {
            return;
        }

        string rendered = string.Format(CultureInfo.InvariantCulture, NormalizeFormatString(format, args.Length), args);
        LogMessageCore(logger, level, eventId, rendered, exception);
    }

    private static string NormalizeFormatString(string format, int argumentCount)
    {
        if (argumentCount == 0 || format.IndexOf('{') < 0)
        {
            return format;
        }

        System.Text.StringBuilder builder = new(format.Length + 16);
        int nextArgumentIndex = 0;

        for (int i = 0; i < format.Length; i++)
        {
            char c = format[i];

            if (c == '{')
            {
                if (i + 1 < format.Length && format[i + 1] == '{')
                {
                    builder.Append("{{");
                    i++;
                    continue;
                }

                int close = format.IndexOf('}', i + 1);
                if (close < 0)
                {
                    builder.Append(c);
                    continue;
                }

                ReadOnlySpan<char> token = format.AsSpan(i + 1, close - i - 1);
                int separatorIndex = token.IndexOfAny(',', ':');
                ReadOnlySpan<char> namePart = separatorIndex >= 0 ? token[..separatorIndex] : token;

                if (TryParseIndexedPlaceholder(namePart, out _))
                {
                    builder.Append('{');
                    builder.Append(token);
                    builder.Append('}');
                }
                else if (nextArgumentIndex < argumentCount)
                {
                    builder.Append('{');
                    builder.Append(nextArgumentIndex.ToString(CultureInfo.InvariantCulture));
                    if (separatorIndex >= 0)
                    {
                        builder.Append(token[separatorIndex..]);
                    }
                    builder.Append('}');
                    nextArgumentIndex++;
                }
                else
                {
                    builder.Append('{');
                    builder.Append(token);
                    builder.Append('}');
                }

                i = close;
                continue;
            }

            if (c == '}' && i + 1 < format.Length && format[i + 1] == '}')
            {
                builder.Append("}}");
                i++;
                continue;
            }

            builder.Append(c);
        }

        return builder.ToString();
    }

    private static bool TryParseIndexedPlaceholder(ReadOnlySpan<char> value, out int index)
    {
        index = 0;
        if (value.IsEmpty)
        {
            return false;
        }

        foreach (char c in value)
        {
            if (!char.IsAsciiDigit(c))
            {
                return false;
            }

            index = (index * 10) + (c - '0');
        }

        return true;
    }

    private static void LogMessage(ILogger logger, LogLevel level, EventId? eventId, string message, Exception? exception)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(message);

        if (!logger.IsEnabled(level))
        {
            return;
        }

        LogMessageCore(logger, level, eventId, message, exception);
    }

    private static void LogMessageCore(ILogger logger, LogLevel level, EventId? eventId, string message, Exception? exception)
    {
        MessageLogState state = new(message);

        if (eventId.HasValue)
        {
            logger.Log(level, eventId.Value, state, exception, s_messageFormatter);
            return;
        }

        logger.Log(level, default, state, exception, s_messageFormatter);
    }

    private readonly record struct MessageLogState(string Message);
}
