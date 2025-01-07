using Notio.Logging.Enums;
using Notio.Logging.Metadata;
using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;

namespace Notio.Logging.Format;

internal static class LoggingBuilder
{
    private const int DefaultBufferSize = 256;

    // Cache các ký tự thường xuyên sử dụng
    private const char OpenBracket = '[';

    private const char CloseBracket = ']';
    private const char Separator = '\t';
    private const char Dash = '-';
    private const char Colon = ':';

    private static readonly ArrayPool<char> CharPool = ArrayPool<char>.Shared;

    // Sử dụng ReadOnlySpan để tối ưu memory
    private static ReadOnlySpan<string> LogLevelStrings => new[]
    {
        "TRCE", // LoggingLevel.Trace (0)
        "DBUG", // LoggingLevel.Debug (1)
        "INFO", // LoggingLevel.Information (2)
        "WARN", // LoggingLevel.Warning (3)
        "FAIL", // LoggingLevel.Error (4)
        "CRIT", // LoggingLevel.Critical (5)
        "NONE"  // LoggingLevel.None (6)
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string GetShortLogLevel(LoggingLevel logLevel)
    {
        int index = (int)logLevel;
        return (uint)index < (uint)LogLevelStrings.Length
            ? LogLevelStrings[index]
            : logLevel.ToString().ToUpperInvariant();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void BuildLog(
        StringBuilder builder,
        in DateTime timeStamp,
        LoggingLevel logLevel,
        in EventId eventId,
        string message,
        Exception? exception)
    {
        // Estimate buffer size to minimize reallocations
        int estimatedLength = CalculateEstimatedLength(message, eventId, exception);
        EnsureCapacity(builder, estimatedLength);

        AppendTimestamp(builder, timeStamp);
        AppendSeparator(builder);
        AppendLogLevel(builder, logLevel);
        AppendSeparator(builder);
        AppendEventId(builder, eventId);
        AppendSeparator(builder);
        AppendMessage(builder, message);

        if (exception is not null) AppendException(builder, exception);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AppendTimestamp(StringBuilder builder, in DateTime timeStamp)
    {
        Span<char> dateBuffer = stackalloc char[23]; // yyyy-MM-dd HH:mm:ss.fff
        if (timeStamp.TryFormat(dateBuffer, out int charsWritten, "yyyy-MM-dd HH:mm:ss.fff"))
        {
            builder.Append(OpenBracket)
                   .Append(dateBuffer[..charsWritten])
                   .Append(CloseBracket);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AppendSeparator(StringBuilder builder)
    {
        builder.Append(Separator)
               .Append(Dash)
               .Append(Separator);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AppendLogLevel(StringBuilder builder, LoggingLevel logLevel)
    {
        builder.Append(OpenBracket)
               .Append(GetShortLogLevel(logLevel))
               .Append(CloseBracket);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AppendEventId(StringBuilder builder, in EventId eventId)
    {
        builder.Append(OpenBracket);

        if (eventId.Name is not null)
        {
            builder.Append(eventId.Id)
                   .Append(Colon)
                   .Append(eventId.Name);
        }
        else
        {
            builder.Append(eventId.Id);
        }

        builder.Append(CloseBracket);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AppendMessage(StringBuilder builder, string message)
    {
        builder.Append(OpenBracket)
               .Append(message)
               .Append(CloseBracket);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AppendException(StringBuilder builder, Exception exception)
    {
        builder.Append(Separator)
               .Append(Dash)
               .Append(Separator)
               .AppendLine()
               .Append(exception);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CalculateEstimatedLength(string message, in EventId eventId, Exception? exception)
    {
        int length = DefaultBufferSize + message.Length;

        if (eventId.Name is not null)
        {
            length += eventId.Name.Length;
        }

        if (exception is not null)
        {
            length += exception.ToString().Length;
        }

        return length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EnsureCapacity(StringBuilder builder, int capacity)
    {
        if (builder.Capacity < capacity)
        {
            builder.EnsureCapacity(capacity);
        }
    }
}