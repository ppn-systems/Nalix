using Notio.Common.Logging;
using Notio.Logging.Enums;
using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;

namespace Notio.Logging.Format;

internal static class LoggingBuilder
{
    private const int DefaultBufferSize = 256;
    private const char OpenBracket = '[';
    private const char CloseBracket = ']';
    private const string DefaultSeparator = "   -   ";
    private static readonly ArrayPool<char> CharPool = ArrayPool<char>.Shared;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void BuildLog(
        StringBuilder builder,
        in DateTime timeStamp,
        LoggingLevel logLevel,
        in EventId eventId,
        string message,
        Exception? exception,
        string separator = DefaultSeparator,
        string? customTimestampFormat = null)
    {
        // Estimate buffer size to minimize reallocations
        int estimatedLength = CalculateEstimatedLength(message, eventId, exception);
        EnsureCapacity(builder, estimatedLength);

        AppendTimestamp(builder, timeStamp, customTimestampFormat);
        AppendSeparator(builder, separator);
        AppendLogLevel(builder, logLevel);
        AppendSeparator(builder, separator);

        if (eventId.Id != 0)
        {
            AppendEventId(builder, eventId);
            AppendSeparator(builder, separator);
        }

        AppendMessage(builder, message);

        if (exception is not null)
        {
            AppendException(builder, exception, separator);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AppendTimestamp(StringBuilder builder, in DateTime timeStamp, string? format)
    {
        // Default to "yyyy-MM-dd HH:mm:ss.fff" if no custom format is provided
        string timestampFormat = format ?? "yyyy-MM-dd HH:mm:ss.fff";
        Span<char> dateBuffer = stackalloc char[timestampFormat.Length + 10];

        if (timeStamp.TryFormat(dateBuffer, out int charsWritten, timestampFormat))
        {
            builder.Append(OpenBracket)
                   .Append(dateBuffer[..charsWritten])
                   .Append(CloseBracket);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AppendSeparator(StringBuilder builder, string separator) =>
        builder.Append(separator);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AppendLogLevel(StringBuilder builder, LoggingLevel logLevel) =>
        builder.Append(OpenBracket)
               .Append(LoggingLevelFormatter.GetShortLogLevel(logLevel))
               .Append(CloseBracket);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AppendEventId(StringBuilder builder, in EventId eventId)
    {
        builder.Append(OpenBracket);

        if (eventId.Name is not null)
        {
            builder.Append(eventId.Id)
                   .Append(':')
                   .Append(eventId.Name);
        }
        else
        {
            builder.Append(eventId.Id);
        }

        builder.Append(CloseBracket);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AppendMessage(StringBuilder builder, string message) =>
        builder.Append(OpenBracket)
               .Append(message)
               .Append(CloseBracket);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AppendException(StringBuilder builder, Exception exception, string separator) =>
        builder.Append(separator)
               .AppendLine()
               .Append(exception);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CalculateEstimatedLength(string message, in EventId eventId, Exception? exception)
    {
        int length = DefaultBufferSize + message.Length;

        if (eventId.Name is not null)
            length += eventId.Name.Length;

        if (exception is not null)
            length += exception.ToString().Length;

        return length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EnsureCapacity(StringBuilder builder, int capacity)
    {
        if (builder.Capacity < capacity)
            builder.EnsureCapacity(capacity);
    }
}