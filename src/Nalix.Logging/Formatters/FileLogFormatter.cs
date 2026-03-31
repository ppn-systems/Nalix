using System;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Nalix.Logging.Internal.Formatters;
using Nalix.Logging.Internal.Pooling;

namespace Nalix.Logging.Formatters;

/// <inheritdoc/>
public sealed class FileLogFormatter : INLogixFormatter
{
    private const string TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff";

    /// <inheritdoc/>
    public string Format(
        DateTime timestampUtc,
        LogLevel logLevel,
        EventId eventId,
        string message,
        Exception? exception)
    {
        ArgumentNullException.ThrowIfNull(message);

        string timestamp = TimestampCache.GetFormattedTimestamp(timestampUtc, TimestampFormat);
        bool hasEventId = eventId != default;
        bool hasException = exception is not null;

        return string.Create(
            timestamp.Length + message.Length + 64 + (exception?.ToString().Length ?? 0),
            (timestamp, logLevel, eventId, message, exception, hasEventId, hasException),
            (span, state) =>
            {
                (string ts, LogLevel level, EventId evt, string msg, Exception? ex, bool hasEvt, bool hasEx) = state;

                int pos = 0;
                span[pos++] = '[';
                ts.AsSpan().CopyTo(span[pos..]);
                pos += ts.Length;
                span[pos++] = ']';
                span[pos++] = ' ';

                if (hasEvt)
                {
                    span[pos++] = '[';
                    _ = evt.Id.TryFormat(span[pos..], out int written, provider: CultureInfo.InvariantCulture);
                    pos += written;
                    span[pos++] = ']';
                    span[pos++] = ' ';
                }

                span[pos++] = '[';
                ReadOnlySpan<char> levelSpan = LogLevelShortNames.GetShortName(level);
                levelSpan.CopyTo(span[pos..]);
                pos += levelSpan.Length;
                span[pos++] = ']';
                span[pos++] = ' ';

                msg.AsSpan().CopyTo(span[pos..]);
                pos += msg.Length;

                if (hasEx && ex is not null)
                {
                    const string prefix = " - Exception: ";
                    prefix.AsSpan().CopyTo(span[pos..]);
                    pos += prefix.Length;

                    string exStr = ex.ToString();
                    exStr.AsSpan().CopyTo(span[pos..]);
                }
            });
    }

    /// <inheritdoc/>
    public void Format(
        DateTime timestampUtc,
        LogLevel logLevel,
        EventId eventId,
        string message,
        Exception? exception,
        StringBuilder sb)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(sb);

        _ = sb.Append('[')
              .Append(TimestampCache.GetFormattedTimestamp(timestampUtc, TimestampFormat))
              .Append(']')
              .Append(' ');

        if (eventId != default)
        {
            _ = sb.Append('[')
                  .Append(eventId.Id)
                  .Append(']')
                  .Append(' ');
        }

        _ = sb.Append('[')
              .Append(LogLevelShortNames.GetShortName(logLevel))
              .Append(']')
              .Append(' ')
              .Append(message);

        if (exception is not null)
        {
            _ = sb.Append(" - Exception: ")
                  .Append(exception);
        }
    }
}
