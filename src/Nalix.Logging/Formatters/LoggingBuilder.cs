using Nalix.Common.Logging;
using Nalix.Logging.Formatters.Internal;
using System;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Nalix.Logging.Formatters;

/// <summary>
/// High-performance log building utilities optimized for minimal allocations.
/// </summary>
internal static class LoggingBuilder
{
    #region Constants

    // Use buffer sizes that are powers of 2 for optimization
    private const int SmallMessageBufferSize = 256;

    private const int MediumMessageBufferSize = 512;
    private const int LargeMessageBufferSize = 1024;

    #endregion Constants

    #region Fields

    // Preallocated arrays for common separators to avoid string allocations
    private static ReadOnlySpan<char> DashWithSpaces => [' ', '-', ' '];

    private static int LogCounter = 0;

    #endregion Fields

    #region Public Methods

    /// <summary>
    /// Builds a formatted log entry with optimal performance.
    /// </summary>
    /// <param name="builder">The StringBuilder to append the log to.</param>
    /// <param name="timeStamp">The timestamp of the log entry.</param>
    /// <param name="logLevel">The logging level.</param>
    /// <param name="eventId">The event Number associated with the log entry.</param>
    /// <param name="message">The log message.</param>
    /// <param name="exception">Optional exception information.</param>
    /// <param name="colors">Whether to include ANSI color codes in the output.</param>
    /// <param name="customTimestampFormat">Custom timestamp format or null to use default.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void BuildLog(
        StringBuilder builder, in DateTime timeStamp, LogLevel logLevel,
        in EventId eventId, string message, Exception? exception,
        bool colors = false, string? customTimestampFormat = null)
    {
        // Estimate buffer size to minimize reallocations
        int initialLength = builder.Length;
        int estimatedLength = CalculateEstimatedLength(message, eventId, exception, colors);

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

    /// <summary>
    /// Creates a fully formatted log entry and returns it as a string.
    /// </summary>
    /// <param name="timeStamp">The timestamp of the log entry.</param>
    /// <param name="logLevel">The logging level.</param>
    /// <param name="eventId">The event Number associated with the log entry.</param>
    /// <param name="message">The log message.</param>
    /// <param name="exception">Optional exception information.</param>
    /// <param name="colors">Whether to include ANSI color codes in the output.</param>
    /// <param name="customTimestampFormat">Custom timestamp format or null to use default.</param>
    /// <returns>A formatted log message.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string CreateLogMessage(
        in DateTime timeStamp, LogLevel logLevel, in EventId eventId,
        string message, Exception? exception, bool colors = false,
        string? customTimestampFormat = null)
    {
        // Determine appropriate buffer size based on message
        int bufferSize = message.Length < 100 ? SmallMessageBufferSize :
                         message.Length < 500 ? MediumMessageBufferSize :
                         LargeMessageBufferSize;

        // Use pooled StringBuilder to reduce memory pressure
        var builder = StringBuilderPool.Rent(bufferSize);
        try
        {
            BuildLog(builder, timeStamp, logLevel, eventId, message, exception, colors, customTimestampFormat);
            return builder.ToString();
        }
        finally
        {
            StringBuilderPool.Return(builder);
        }
    }

    #endregion Public Methods

    #region Utility Methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AppendNumber(StringBuilder builder, bool colors)
    {
        if (!colors) return;

        builder.Append(LoggingConstants.LogBracketOpen)
               .Append(Interlocked.Increment(ref LogCounter).ToString("D6"))
               .Append(LoggingConstants.LogBracketClose)
               .Append(LoggingConstants.LogSpaceSeparator);
    }

    /// <summary>
    /// Appends a formatted timestamp to the string builder.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AppendTimestamp(
        StringBuilder builder, in DateTime timeStamp, string? format, bool colors)
    {
        string timestampFormat = format ?? "yyyy-MM-dd HH:mm:ss.fff";

        // Allocate buffer on the stack for datetime formatting
        Span<char> dateBuffer = stackalloc char[timestampFormat.Length + 10];

        // Format timestamp directly into stack-allocated buffer
        if (timeStamp.TryFormat(dateBuffer, out int charsWritten, timestampFormat))
        {
            builder.Append(LoggingConstants.LogBracketOpen);

            if (colors)
                builder.Append(ColorAnsi.Blue);

            // Append directly from the span to avoid string allocation
            builder.Append(dateBuffer[..charsWritten]);

            if (colors)
                builder.Append(ColorAnsi.White);

            builder.Append(LoggingConstants.LogBracketClose);
        }
    }

    /// <summary>
    /// Appends a formatted log level to the string builder.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AppendLogLevel(StringBuilder builder, LogLevel logLevel, bool colors)
    {
        builder.Append(LoggingConstants.LogSpaceSeparator)
               .Append(LoggingConstants.LogBracketOpen);

        if (colors)
            builder.Append(ColorAnsi.GetColorCode(logLevel));

        // Use span-based API for log level text to avoid string allocation
        ReadOnlySpan<char> levelText = LoggingLevelFormatter.GetShortLogLevel(logLevel);
        builder.Append(levelText);

        if (colors)
            builder.Append(ColorAnsi.White);

        builder.Append(LoggingConstants.LogBracketClose);
    }

    /// <summary>
    /// Appends a formatted event Number to the string builder if it exists.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AppendEventId(StringBuilder builder, in EventId eventId, bool colors)
    {
        // Skip if it's the empty event Number
        if (eventId.Id == 0) return;

        builder.Append(LoggingConstants.LogSpaceSeparator)
               .Append(LoggingConstants.LogBracketOpen);

        if (colors && eventId.Name != null)
            builder.Append(ColorAnsi.Blue);

        // Append Number
        builder.Append(eventId.Id);

        // Append name if present
        if (eventId.Name != null)
        {
            builder.Append(':')
                   .Append(eventId.Name);
        }

        if (colors && eventId.Name != null)
            builder.Append(ColorAnsi.White);

        builder.Append(LoggingConstants.LogBracketClose);
    }

    /// <summary>
    /// Appends a formatted message to the string builder.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AppendMessage(StringBuilder builder, string message, bool colors)
    {
        builder.Append(LoggingConstants.LogSpaceSeparator);

        // Use span-based append for standard separators
        builder.Append(DashWithSpaces);

        if (colors)
            builder.Append(ColorAnsi.DarkGray);

        builder.Append(message);

        if (colors)
            builder.Append(ColorAnsi.White);
    }

    /// <summary>
    /// Appends exception details to the string builder if an exception exists.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AppendException(StringBuilder builder, Exception? exception, bool colors)
    {
        if (exception == null) return;

        builder.Append(LoggingConstants.LogSpaceSeparator);
        builder.Append(DashWithSpaces);
        builder.AppendLine();

        if (colors)
            builder.Append(ColorAnsi.Red);

        // For complex exceptions, build a structured representation
        FormatExceptionDetails(builder, exception);

        if (colors)
            builder.Append(ColorAnsi.White);
    }

    /// <summary>
    /// Formats exception details with a structured approach for better readability.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FormatExceptionDetails(StringBuilder builder, Exception exception, int level = 0)
    {
        // Indent based on level for inner exceptions
        if (level > 0)
        {
            builder.Append(' ', level * 2)
                   .Append("> ");
        }

        // Append exception type and message
        builder.Append(exception.GetType().Name)
               .Append(": ")
               .AppendLine(exception.Message);

        // Add stack trace with indentation
        if (!string.IsNullOrEmpty(exception.StackTrace))
        {
            string[] stackFrames = exception.StackTrace.Split('\n');
            foreach (string frame in stackFrames)
            {
                if (string.IsNullOrWhiteSpace(frame)) continue;

                builder.Append(' ', (level + 1) * 2)
                       .AppendLine(frame.TrimStart());
            }
        }

        // Handle inner exceptions recursively
        if (exception.InnerException != null)
        {
            builder.AppendLine()
                   .Append(' ', level * 2)
                   .AppendLine("Caused by: ");

            FormatExceptionDetails(builder, exception.InnerException, level + 1);
        }

        // Handle aggregate exceptions
        if (exception is AggregateException aggregateException &&
            aggregateException.InnerExceptions.Count > 1)
        {
            for (int i = 0; i < aggregateException.InnerExceptions.Count; i++)
            {
                if (i == 0) continue; // Skip first one as it's already handled as InnerException

                builder.AppendLine()
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CalculateEstimatedLength(
        string message, in EventId eventId, Exception? exception, bool colors)
    {
        // Base size includes timestamp format, brackets, and separators
        int length = LoggingConstants.DefaultLogBufferSize + message.Length;

        // Add event Number length if present
        if (eventId.Id != 0)
        {
            length += 5; // Brackets, space, and typical Number length
            if (eventId.Name != null)
                length += eventId.Name.Length + 1; // +1 for colon
        }

        // Add exception length if present
        if (exception != null)
        {
            // Estimate exception length based on type and message
            length += exception.GetType().Name.Length + exception.Message.Length + 20;

            // Add some space for stack trace (rough estimate)
            if (exception.StackTrace != null)
                length += Math.Min(exception.StackTrace.Length, 500);
        }

        // Add space for color codes if used
        if (colors)
            length += 30; // Approximate length of all color codes

        return length;
    }

    /// <summary>
    /// Ensures the StringBuilder has sufficient capacity, expanding only when necessary.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EnsureCapacity(StringBuilder builder, int requiredCapacity)
    {
        if (builder.Capacity < requiredCapacity)
        {
            // Grow exponentially but with a cap to avoid excessive allocations
            int newCapacity = Math.Min(
                Math.Max(requiredCapacity, builder.Capacity * 2),
                builder.Capacity + 4096); // Cap growth at 4KB per resize

            builder.EnsureCapacity(newCapacity);
        }
    }

    #endregion Utility Methods
}
