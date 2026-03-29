// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Text;
using Nalix.Common.Diagnostics;
using Nalix.Logging.Engine;
using Nalix.Logging.Internal.Pooling;

#if DEBUG
[assembly: InternalsVisibleTo("Nalix.Logging.Tests")]
[assembly: InternalsVisibleTo("Nalix.Logging.Benchmarks")]
#endif

namespace Nalix.Logging.Internal.Formatters;

/// <summary>
/// High-performance log building utilities optimized for minimal allocations.
/// </summary>
[DebuggerNonUserCode]
[ExcludeFromCodeCoverage]
internal static class LogMessageBuilder
{
    #region Public Methods

    /// <summary>
    /// Builds a formatted log entry with optimal performance.
    /// </summary>
    /// <param name="builder">The StringBuilder to append the log to.</param>
    /// <param name="timeStamp">The timestamp of the log entry.</param>
    /// <param name="logLevel">The logging level.</param>
    /// <param name="eventId">The event ProtocolType associated with the log entry.</param>
    /// <param name="message">The log message.</param>
    /// <param name="exception">Optional exception information.</param>
    /// <param name="colors">Whether to include ANSI color codes in the output.</param>
    /// <param name="customTimestampFormat">Custom timestamp format or null to use default.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void AppendFormatted(
        StringBuilder builder,
        in DateTime timeStamp, LogLevel logLevel,
        in EventId eventId, string message, Exception? exception,
        bool colors = false, string? customTimestampFormat = null)
    {
        // Estimate buffer size to minimize reallocations
        int initialLength = builder.Length;
        int estimatedLength = CalculateEstimatedLength(message, eventId, exception, colors);

        // Ensure the builder has enough capacity
        if (colors)
        {
            _ = builder.Append(AnsiColors.White);
            EnsureCapacity(builder, estimatedLength + initialLength + 9);
            AppendTimestamp(builder, timeStamp, customTimestampFormat, true);
            AppendLogLevel(builder, logLevel, true);
            AppendEventId(builder, eventId, true);
            AppendMessage(builder, message);
            AppendException(builder, exception, true);
            _ = builder.Append(AnsiColors.Reset);
        }
        else
        {
            EnsureCapacity(builder, estimatedLength + initialLength + 9);

            // Append each part efficiently
            //AppendNumber(builder, colors);
            AppendTimestamp(builder, timeStamp, customTimestampFormat, false);
            AppendLogLevel(builder, logLevel, false);
            AppendEventId(builder, eventId, false);
            AppendMessage(builder, message);
            AppendException(builder, exception, false);
        }

    }

    #endregion Public Methods

    #region Private Methods

    /// <summary>
    /// Appends a formatted timestamp to the string builder.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void AppendTimestamp(StringBuilder builder, in DateTime timeStamp, string? format, bool colors)
    {
        string timestampFormat = format ?? "HH:mm:ss.fff";

        // Use timestamp cache for better performance
        string formattedTimestamp = TimestampCache.GetFormattedTimestamp(timeStamp, timestampFormat);

        _ = builder.Append(InternCache.BracketOpen);

        if (colors)
        {
            _ = builder.Append(AnsiColors.Blue);
            _ = builder.Append(formattedTimestamp);
            _ = builder.Append(AnsiColors.White);
        }
        else
        {
            _ = builder.Append(formattedTimestamp);
        }

        _ = builder.Append(InternCache.BracketClose);
    }

    /// <summary>
    /// Appends a formatted log level to the string builder.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void AppendLogLevel(StringBuilder builder, LogLevel logLevel, bool colors)
    {
        _ = builder.Append(InternCache.Space)
                   .Append(InternCache.BracketOpen);

        ReadOnlySpan<char> levelText = LogLevelShortNames.GetShortName(logLevel);

        if (colors)
        {
            _ = builder.Append(AnsiColors.GetForLevel(logLevel));
            _ = builder.Append(levelText);
            _ = builder.Append(AnsiColors.White);
        }
        else
        {
            _ = builder.Append(levelText);
        }

        _ = builder.Append(InternCache.BracketClose);
    }

    /// <summary>
    /// Appends a formatted event ProtocolType to the string builder if it exists.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void AppendEventId(StringBuilder builder, in EventId eventId, bool colors)
    {
        if (eventId.Id == 0)
        {
            return;
        }

        _ = builder.Append(InternCache.Space)
                   .Append(InternCache.BracketOpen);

        if (colors)
        {
            // Colored path
            _ = builder.Append(AnsiColors.Cyan)
                       .Append(eventId.Id);

            if (!string.IsNullOrEmpty(eventId.Name))
            {
                _ = builder.Append(AnsiColors.White)
                           .Append(InternCache.Colon)
                           .Append(AnsiColors.DarkGray)
                           .Append(eventId.Name);
            }

            _ = builder.Append(AnsiColors.White);
        }
        else
        {
            // Plain path
            _ = builder.Append(eventId.Id);
            if (!string.IsNullOrEmpty(eventId.Name))
            {
                _ = builder.Append(InternCache.Colon)
                           .Append(eventId.Name);
            }
        }

        _ = builder.Append(InternCache.BracketClose);
    }

    /// <summary>
    /// Appends a formatted message to the string builder.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void AppendMessage(StringBuilder builder, string message)
    {
        // Use interned string for separator
        _ = builder.Append(InternCache.DashWithSpaces);
        _ = builder.Append(message);
    }

    /// <summary>
    /// Appends an exception to the builder. Never throws (best-effort).
    /// </summary>
    private static void AppendException(StringBuilder builder, Exception? exception, bool colors)
    {
        if (exception is null)
        {
            return;
        }

        try
        {
            _ = builder.AppendLine(InternCache.DashWithSpaces);

            if (colors)
            {
                // Header in red, details in white, then reset once.
                _ = builder.Append(AnsiColors.Red);
                WriteHeader(builder, exception);
                _ = builder.Append(AnsiColors.White);

                // Details: skip header at level 0 to avoid duplication.
                FormatExceptionDetails(builder, exception, level: 0, includeHeader: false);
            }
            else
            {
                WriteHeader(builder, exception);
                FormatExceptionDetails(builder, exception, level: 0, includeHeader: false);
            }
        }
        catch
        {
            // Swallow to guarantee logger never crashes the app.
            // Minimal fallback (no colors).
            try
            {
                _ = builder.Append("Logger failed to format exception: ")
                           .Append(exception?.GetType().Name)
                           .Append(InternCache.Colon)
                           .Append(InternCache.Space)
                           .AppendLine(exception?.Message);
            }
            catch { /* last resort: ignore */ }
        }
    }

    /// <summary>
    /// Writes the exception header: "Type: Message".
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteHeader(StringBuilder builder, Exception ex)
    {
        _ = builder.Append(ex.GetType().Name)
                   .Append(InternCache.Colon)
                   .Append(InternCache.Space)
                   .AppendLine(ex.Message);
    }

    /// <summary>
    /// Formats stack trace and inner exceptions. Optionally writes header per level.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void FormatExceptionDetails(
        StringBuilder builder, Exception exception,
        int level = 0, bool includeHeader = true)
    {
        // Indent for inner exceptions
        if (level > 0)
        {
            _ = builder.Append(' ', level * 2).Append("> ");
        }

        if (includeHeader)
        {
            WriteHeader(builder, exception);
        }

        // Stack trace (handle both \n and \r\n; skip empty lines)
        string? stack = exception.StackTrace;
        if (!string.IsNullOrEmpty(stack))
        {
            AppendStackTraceLines(builder, stack.AsSpan(), level);
        }

        // AggregateException: enumerate all inner exceptions explicitly
        if (exception is AggregateException agg && agg.InnerExceptions.Count > 0)
        {
            for (int i = 0; i < agg.InnerExceptions.Count; i++)
            {
                _ = builder.AppendLine()
                           .Append(' ', level * 2)
                           .Append("Caused by [")
                           .Append(i + 1)
                           .Append('/')
                           .Append(agg.InnerExceptions.Count)
                           .AppendLine("]:");

                FormatExceptionDetails(builder, agg.InnerExceptions[i], level + 1, includeHeader: true);
            }
            return;
        }

        // Single InnerException (non-aggregate)
        if (exception.InnerException is not null)
        {
            _ = builder.AppendLine()
                       .Append(' ', level * 2)
                       .AppendLine("Caused by:");

            FormatExceptionDetails(builder, exception.InnerException, level + 1, includeHeader: true);
        }
    }

    /// <summary>
    /// Appends stack trace lines without allocating an intermediate string array.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void AppendStackTraceLines(StringBuilder builder, ReadOnlySpan<char> stack, int level)
    {
        int start = 0;
        while (start < stack.Length)
        {
            int relativeEnd = stack.Slice(start).IndexOf('\n');
            int length = relativeEnd < 0 ? stack.Length - start : relativeEnd;

            ReadOnlySpan<char> line = stack.Slice(start, length);
            if (!line.IsEmpty && line[^1] == '\r')
            {
                line = line[..^1];
            }

            line = line.TrimStart();
            if (!line.IsEmpty)
            {
                _ = builder.Append(' ', (level + 1) * 2)
                           .Append(line)
                           .AppendLine();
            }

            start += length + 1;
        }
    }

    /// <summary>
    /// Calculates the estimated buffer size needed for the complete log message.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static int CalculateEstimatedLength(
        string message, in EventId eventId,
        Exception? exception, bool colors)
    {
        // Base size includes timestamp format, brackets, and separators
        int length = NLogixConstants.DefaultLogBufferSize + message.Length;

        // Add event ProtocolType length if present
        if (eventId.Id != 0)
        {
            length += 5; // Brackets, space, and typical ProtocolType length
            if (eventId.Name != null)
            {
                length += eventId.Name.Length + 1; // +1 for colon
            }
        }

        // Add exception length if present
        if (exception != null)
        {
            // Estimate exception length based on type and message
            length += exception.GetType().Name.Length + exception.Message.Length + 20;

            // Add some space for stack trace (rough estimate)
            if (exception.StackTrace != null)
            {
                length += Math.Min(exception.StackTrace.Length, 500);
            }
        }

        // Add space for color codes if used
        if (colors)
        {
            length += 30; // Approximate length of all color codes
        }

        return length;
    }

    /// <summary>
    /// Ensures the StringBuilder has sufficient capacity, expanding only when necessary.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void EnsureCapacity(StringBuilder builder, int requiredCapacity)
    {
        if (builder.Capacity < requiredCapacity)
        {
            // Grow exponentially but with a cap to avoid excessive allocations
            int newCapacity = Math.Min(
                Math.Max(requiredCapacity, builder.Capacity * 2),
                builder.Capacity + 4096); // Cap growth at 4KB per resize

            _ = builder.EnsureCapacity(newCapacity);
        }
    }

    #endregion Private Methods
}
