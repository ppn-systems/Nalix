using Nalix.Common.Logging;
using Nalix.Logging.Formatters.Internal;

namespace Nalix.Logging.Formatters;

/// <summary>
/// High-performance log building utilities optimized for minimal allocations.
/// </summary>
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
    /// <param name="eventId">The event Number associated with the log entry.</param>
    /// <param name="message">The log message.</param>
    /// <param name="exception">Optional exception information.</param>
    /// <param name="colors">Whether to include ANSI color codes in the output.</param>
    /// <param name="customTimestampFormat">Custom timestamp format or null to use default.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void BuildLog(
        System.Text.StringBuilder builder,
        in System.DateTime timeStamp, LogLevel logLevel,
        in EventId eventId, System.String message, System.Exception? exception,
        System.Boolean colors = false, System.String? customTimestampFormat = null)
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.String CreateLogMessage(
        in System.DateTime timeStamp, LogLevel logLevel, in EventId eventId,
        System.String message, System.Exception? exception, System.Boolean colors = false,
        System.String? customTimestampFormat = null)
    {
        // Determine appropriate buffer size based on message
        System.Int32 bufferSize = message.Length < 100 ? SmallMessageBufferSize :
                         message.Length < 500 ? MediumMessageBufferSize :
                         LargeMessageBufferSize;

        // Use pooled StringBuilder to reduce memory pressure
        System.Text.StringBuilder builder = StringBuilderPool.Rent(bufferSize);
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

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void AppendNumber(
        System.Text.StringBuilder builder, System.Boolean colors)
    {
        if (!colors) return;

        builder.Append(LoggingConstants.LogBracketOpen)
               .Append(System.Threading.Interlocked.Increment(ref LogCounter).ToString("D6"))
               .Append(LoggingConstants.LogBracketClose)
               .Append(LoggingConstants.LogSpaceSeparator);
    }

    /// <summary>
    /// Appends a formatted timestamp to the string builder.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void AppendTimestamp(
        System.Text.StringBuilder builder,
        in System.DateTime timeStamp, System.String? format, System.Boolean colors)
    {
        System.String timestampFormat = format ?? "yyyy-MM-dd HH:mm:ss.fff";

        // Allocate buffer on the stack for datetime formatting
        System.Span<char> dateBuffer = stackalloc System.Char[timestampFormat.Length + 10];

        // Format timestamp directly into stack-allocated buffer
        if (timeStamp.TryFormat(dateBuffer, out System.Int32 charsWritten, timestampFormat))
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void AppendLogLevel(
        System.Text.StringBuilder builder,
        LogLevel logLevel, System.Boolean colors)
    {
        builder.Append(LoggingConstants.LogSpaceSeparator)
               .Append(LoggingConstants.LogBracketOpen);

        if (colors)
            builder.Append(ColorAnsi.GetColorCode(logLevel));

        // Use span-based API for log level text to avoid string allocation
        System.ReadOnlySpan<System.Char> levelText = LoggingLevelFormatter.GetShortLogLevel(logLevel);
        builder.Append(levelText);

        if (colors)
            builder.Append(ColorAnsi.White);

        builder.Append(LoggingConstants.LogBracketClose);
    }

    /// <summary>
    /// Appends a formatted event Number to the string builder if it exists.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void AppendEventId(
        System.Text.StringBuilder builder,
        in EventId eventId, System.Boolean colors)
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void AppendMessage(
        System.Text.StringBuilder builder,
        System.String message, System.Boolean colors)
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void AppendException(
        System.Text.StringBuilder builder,
        System.Exception? exception, System.Boolean colors)
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void FormatExceptionDetails(
        System.Text.StringBuilder builder,
        System.Exception exception, System.Int32 level = 0)
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
        if (exception is System.AggregateException aggregateException &&
            aggregateException.InnerExceptions.Count > 1)
        {
            for (System.Int32 i = 0; i < aggregateException.InnerExceptions.Count; i++)
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Int32 CalculateEstimatedLength(
        System.String message, in EventId eventId,
        System.Exception? exception, System.Boolean colors)
    {
        // Base size includes timestamp format, brackets, and separators
        System.Int32 length = LoggingConstants.DefaultLogBufferSize + message.Length;

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
                length += System.Math.Min(exception.StackTrace.Length, 500);
        }

        // Add space for color codes if used
        if (colors)
            length += 30; // Approximate length of all color codes

        return length;
    }

    /// <summary>
    /// Ensures the StringBuilder has sufficient capacity, expanding only when necessary.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
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

            builder.EnsureCapacity(newCapacity);
        }
    }

    #endregion Utility Methods
}
