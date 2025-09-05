// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Logging.Models;
using Nalix.Logging.Engine;
using Nalix.Logging.Internal;

namespace Nalix.Logging.Formatters;

/// <summary>
/// High-performance log building utilities optimized for minimal allocations.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
internal static class LoggingBuilder
{
    #region Constants

    // Use buffer sizes that are powers of 2 for optimization

    private const System.Int32 SmallMessageBufferSize = 256;
    private const System.Int32 MediumMessageBufferSize = 512;
    private const System.Int32 LargeMessageBufferSize = 1024;

    #endregion Constants

    #region Fields

    // Preallocated arrays for common separators to avoid string allocations
    private static System.ReadOnlySpan<System.Char> DashWithSpaces => [' ', '-', ' '];

    private static System.Int32 LogCounter = 0;

    #endregion Fields

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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static void BuildLog(
        System.Text.StringBuilder builder,
        in System.DateTime timeStamp, LogLevel logLevel,
        in EventId eventId, System.String message, System.Exception? exception,
        System.Boolean colors = false, System.String? customTimestampFormat = null)
    {
        // Estimate buffer size to minimize reallocations
        System.Int32 initialLength = builder.Length;
        System.Int32 estimatedLength = CalculateEstimatedLength(message, eventId, exception, colors);

        // Ensure the builder has enough capacity
        if (colors)
        {
            _ = builder.Append(ColorAnsi.White);
            EnsureCapacity(builder, estimatedLength + initialLength + 9);
            AppendTimestamp(builder, timeStamp, customTimestampFormat, true);
            AppendLogLevel(builder, logLevel, true);
            AppendEventId(builder, eventId, true);
            AppendMessage(builder, message);
            AppendException(builder, exception, true);
            _ = builder.Append(ColorAnsi.Reset);
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

    #region Utility Methods

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void AppendNumber(
        System.Text.StringBuilder builder, System.Boolean colors)
    {
        if (!colors)
        {
            return;
        }

        _ = builder.Append(LogConstants.LogBracketOpen)
                   .Append(System.Threading.Interlocked.Increment(ref LogCounter).ToString("D6"))
                   .Append(LogConstants.LogBracketClose)
                   .Append(LogConstants.LogSpaceSeparator);
    }

    /// <summary>
    /// Appends a formatted timestamp to the string builder.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void AppendTimestamp(
        System.Text.StringBuilder builder,
        in System.DateTime timeStamp, System.String? format, System.Boolean colors)
    {
        System.String timestampFormat = format ?? "HH:mm:ss.fff";

        // Allocate buffer on the stack for datetime formatting
        System.Span<System.Char> dateBuffer = stackalloc System.Char[timestampFormat.Length];

        // Format timestamp directly into stack-allocated buffer
        if (timeStamp.TryFormat(dateBuffer, out System.Int32 charsWritten, timestampFormat))
        {
            _ = builder.Append(LogConstants.LogBracketOpen);

            if (colors)
            {
                _ = builder.Append(ColorAnsi.Blue);
                _ = builder.Append(dateBuffer[..charsWritten]);
                _ = builder.Append(ColorAnsi.White);
            }
            else
            {
                _ = builder.Append(dateBuffer[..charsWritten]);
            }

            _ = builder.Append(LogConstants.LogBracketClose);
        }
    }

    /// <summary>
    /// Appends a formatted log level to the string builder.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void AppendLogLevel(
        System.Text.StringBuilder builder,
        LogLevel logLevel, System.Boolean colors)
    {
        _ = builder.Append(LogConstants.LogSpaceSeparator)
                   .Append(LogConstants.LogBracketOpen);

        System.ReadOnlySpan<System.Char> levelText = LoggingLevelFormatter.GetShortLogLevel(logLevel);

        if (colors)
        {
            _ = builder.Append(ColorAnsi.GetColorCode(logLevel));
            _ = builder.Append(levelText);
            _ = builder.Append(ColorAnsi.White);
        }
        else
        {
            _ = builder.Append(levelText);
        }

        _ = builder.Append(LogConstants.LogBracketClose);
    }

    /// <summary>
    /// Appends a formatted event ProtocolType to the string builder if it exists.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void AppendEventId(
    System.Text.StringBuilder builder,
    in EventId eventId, System.Boolean colors)
    {
        if (eventId.Id == 0)
        {
            return;
        }

        _ = builder.Append(LogConstants.LogSpaceSeparator)
               .Append(LogConstants.LogBracketOpen);

        if (colors)
        {
            // Colored path
            _ = builder.Append(ColorAnsi.Cyan)
                       .Append(eventId.Id);

            if (!System.String.IsNullOrEmpty(eventId.Name))
            {
                _ = builder.Append(ColorAnsi.White)
                           .Append(':')
                           .Append(ColorAnsi.DarkGray)
                           .Append(eventId.Name);
            }

            _ = builder.Append(ColorAnsi.White);
        }
        else
        {
            // Plain path
            _ = builder.Append(eventId.Id);
            if (!System.String.IsNullOrEmpty(eventId.Name))
            {
                _ = builder.Append(':')
                           .Append(eventId.Name);
            }
        }

        _ = builder.Append(LogConstants.LogBracketClose);
    }

    /// <summary>
    /// Appends a formatted message to the string builder.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void AppendMessage(System.Text.StringBuilder builder, System.String message)
    {
        _ = builder.Append(LogConstants.LogSpaceSeparator);

        // Use span-based append for standard separators
        _ = builder.Append(DashWithSpaces);
        _ = builder.Append(message);
    }

    /// <summary>
    /// Appends an exception to the builder. Never throws (best-effort).
    /// </summary>
    private static void AppendException(
        System.Text.StringBuilder builder,
        System.Exception? exception,
        System.Boolean colors)
    {
        if (exception is null)
        {
            return;
        }

        try
        {
            _ = builder.Append(LogConstants.LogSpaceSeparator)
                       .Append(DashWithSpaces)
                       .AppendLine();

            if (colors)
            {
                // Header in red, details in white, then reset once.
                _ = builder.Append(ColorAnsi.Red);
                WriteHeader(builder, exception);
                _ = builder.Append(ColorAnsi.White);

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
                           .Append(": ")
                           .AppendLine(exception?.Message);
            }
            catch { /* last resort: ignore */ }
        }
    }

    /// <summary>
    /// Writes the exception header: "Type: Message".
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void WriteHeader(System.Text.StringBuilder builder, System.Exception ex)
    {
        _ = builder.Append(ex.GetType().Name)
                   .Append(": ")
                   .AppendLine(ex.Message);
    }

    /// <summary>
    /// Formats stack trace and inner exceptions. Optionally writes header per level.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void FormatExceptionDetails(
        System.Text.StringBuilder builder,
        System.Exception exception,
        System.Int32 level = 0,
        System.Boolean includeHeader = true)
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
        System.String? stack = exception.StackTrace;
        if (!System.String.IsNullOrEmpty(stack))
        {
            var lines = stack.Split(["\r\n", "\n"], System.StringSplitOptions.RemoveEmptyEntries);
            for (System.Int32 i = 0; i < lines.Length; i++)
            {
                System.String line = lines[i].TrimStart();
                if (line.Length == 0)
                {
                    continue;
                }

                _ = builder.Append(' ', (level + 1) * 2)
                           .AppendLine(line);
            }
        }

        // AggregateException: enumerate all inner exceptions explicitly
        if (exception is System.AggregateException agg && agg.InnerExceptions.Count > 0)
        {
            for (System.Int32 i = 0; i < agg.InnerExceptions.Count; i++)
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
    /// Calculates the estimated buffer size needed for the complete log message.
    /// </summary>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static System.Int32 CalculateEstimatedLength(
        System.String message, in EventId eventId,
        System.Exception? exception, System.Boolean colors)
    {
        // Base size includes timestamp format, brackets, and separators
        System.Int32 length = LogConstants.DefaultLogBufferSize + message.Length;

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
                length += System.Math.Min(exception.StackTrace.Length, 500);
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
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void EnsureCapacity(
        System.Text.StringBuilder builder,
        System.Int32 requiredCapacity)
    {
        if (builder.Capacity < requiredCapacity)
        {
            // Grow exponentially but with a cap to avoid excessive allocations
            System.Int32 newCapacity = System.Math.Min(
                System.Math.Max(requiredCapacity, builder.Capacity * 2),
                builder.Capacity + 4096); // Cap growth at 4KB per resize

            _ = builder.EnsureCapacity(newCapacity);
        }
    }

    #endregion Utility Methods
}
