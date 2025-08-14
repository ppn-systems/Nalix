// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Logging;
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
    /// <param name="eventId">The event TransportProtocol associated with the log entry.</param>
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
        EnsureCapacity(builder, estimatedLength + initialLength + 9);

        // Append each part efficiently
        AppendNumber(builder, colors);
        AppendTimestamp(builder, timeStamp, customTimestampFormat, colors);
        AppendLogLevel(builder, logLevel, colors);
        AppendEventId(builder, eventId, colors);
        AppendMessage(builder, message, colors);
        AppendException(builder, exception, colors);
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

        _ = builder.Append(LoggingConstants.LogBracketOpen)
               .Append(System.Threading.Interlocked.Increment(ref LogCounter).ToString("D6"))
               .Append(LoggingConstants.LogBracketClose)
               .Append(LoggingConstants.LogSpaceSeparator);
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
        System.String timestampFormat = format ?? "yyyy-MM-dd HH:mm:ss.fff";

        // Allocate buffer on the stack for datetime formatting
        System.Span<System.Char> dateBuffer = stackalloc System.Char[timestampFormat.Length + 10];

        // Format timestamp directly into stack-allocated buffer
        if (timeStamp.TryFormat(dateBuffer, out System.Int32 charsWritten, timestampFormat))
        {
            _ = builder.Append(LoggingConstants.LogBracketOpen);

            if (colors)
            {
                _ = builder.Append(ColorAnsi.Blue);
            }

            // Append directly from the span to avoid string allocation
            _ = builder.Append(dateBuffer[..charsWritten]);

            if (colors)
            {
                _ = builder.Append(ColorAnsi.White);
            }

            _ = builder.Append(LoggingConstants.LogBracketClose);
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
        _ = builder.Append(LoggingConstants.LogSpaceSeparator)
               .Append(LoggingConstants.LogBracketOpen);

        if (colors)
        {
            _ = builder.Append(ColorAnsi.GetColorCode(logLevel));
        }

        // Use span-based API for log level text to avoid string allocation
        System.ReadOnlySpan<System.Char> levelText = LoggingLevelFormatter.GetShortLogLevel(logLevel);
        _ = builder.Append(levelText);

        if (colors)
        {
            _ = builder.Append(ColorAnsi.White);
        }

        _ = builder.Append(LoggingConstants.LogBracketClose);
    }

    /// <summary>
    /// Appends a formatted event TransportProtocol to the string builder if it exists.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void AppendEventId(
        System.Text.StringBuilder builder,
        in EventId eventId, System.Boolean colors)
    {
        // Skip if it's the empty event TransportProtocol
        if (eventId.Id == 0)
        {
            return;
        }

        _ = builder.Append(LoggingConstants.LogSpaceSeparator)
               .Append(LoggingConstants.LogBracketOpen);

        if (colors && eventId.Name != null)
        {
            _ = builder.Append(ColorAnsi.Blue);
        }

        // Append TransportProtocol
        _ = builder.Append(eventId.Id);

        // Append name if present
        if (eventId.Name != null)
        {
            _ = builder.Append(':')
                   .Append(eventId.Name);
        }

        if (colors && eventId.Name != null)
        {
            _ = builder.Append(ColorAnsi.White);
        }

        _ = builder.Append(LoggingConstants.LogBracketClose);
    }

    /// <summary>
    /// Appends a formatted message to the string builder.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void AppendMessage(
        System.Text.StringBuilder builder,
        System.String message, System.Boolean colors)
    {
        _ = builder.Append(LoggingConstants.LogSpaceSeparator);

        // Use span-based append for standard separators
        _ = builder.Append(DashWithSpaces);

        if (colors)
        {
            _ = builder.Append(ColorAnsi.DarkGray);
        }

        _ = builder.Append(message);

        if (colors)
        {
            _ = builder.Append(ColorAnsi.White);
        }
    }

    /// <summary>
    /// Appends exception details to the string builder if an exception exists.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void AppendException(
        System.Text.StringBuilder builder,
        System.Exception? exception, System.Boolean colors)
    {
        if (exception == null)
        {
            return;
        }

        _ = builder.Append(LoggingConstants.LogSpaceSeparator);
        _ = builder.Append(DashWithSpaces);
        _ = builder.AppendLine();

        if (colors)
        {
            _ = builder.Append(ColorAnsi.Red);
        }

        // For complex exceptions, build a structured representation
        FormatExceptionDetails(builder, exception);

        if (colors)
        {
            _ = builder.Append(ColorAnsi.White);
        }
    }

    /// <summary>
    /// Formats exception details with a structured approach for better readability.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static void FormatExceptionDetails(
        System.Text.StringBuilder builder,
        System.Exception exception, System.Int32 level = 0)
    {
        // Indent based on level for inner exceptions
        if (level > 0)
        {
            _ = builder.Append(' ', level * 2)
                   .Append("> ");
        }

        // Append exception type and message
        _ = builder.Append(exception.GetType().Name)
               .Append(": ")
               .AppendLine(exception.Message);

        // Add stack trace with indentation
        if (!System.String.IsNullOrEmpty(exception.StackTrace))
        {
            System.String[] stackFrames = exception.StackTrace.Split('\n');
            foreach (System.String frame in stackFrames)
            {
                if (System.String.IsNullOrWhiteSpace(frame))
                {
                    continue;
                }

                _ = builder.Append(' ', (level + 1) * 2)
                       .AppendLine(frame.TrimStart());
            }
        }

        // Handle inner exceptions recursively
        if (exception.InnerException != null)
        {
            _ = builder.AppendLine()
                   .Append(' ', level * 2)
                   .AppendLine("Caused by: ");

            FormatExceptionDetails(builder, exception.InnerException, level + 1);
        }

        // Handle aggregate exceptions
        if (exception is System.AggregateException aggregateException &&
            aggregateException.InnerExceptions.Count > 1)
        {
            for (System.Int32 i = 0; i < aggregateException.InnerExceptions.Count; i++)
            {
                if (i == 0)
                {
                    continue; // Skip first one as it's already handled as InnerException
                }

                _ = builder.AppendLine()
                       .Append(' ', level * 2)
                       .Append("Contains ")
                       .Append(i + 1)
                       .Append(" of ")
                       .Append(aggregateException.InnerExceptions.Count)
                       .AppendLine(": ");

                FormatExceptionDetails(builder, aggregateException.InnerExceptions[i], level + 1);
            }
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
        System.Int32 length = LoggingConstants.DefaultLogBufferSize + message.Length;

        // Add event TransportProtocol length if present
        if (eventId.Id != 0)
        {
            length += 5; // Brackets, space, and typical TransportProtocol length
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
