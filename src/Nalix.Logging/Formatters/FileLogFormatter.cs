using System;
using System.Globalization;
using System.Text;
using Nalix.Common.Diagnostics;
using Nalix.Logging.Internal.Formatters;
using Nalix.Logging.Internal.Pooling;

namespace Nalix.Logging.Formatters;

/// <inheritdoc/>
public sealed class FileLogFormatter : ILoggerFormatter
{
    private const string TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff";

    /// <summary>
    /// Formats a <see cref="LogEntry"/> into a plain log string suitable for file output, 
    /// including timestamp, event ID, log level, message, and exception details (if any).
    /// Uses <c>string.Create</c> for high-performance zero-allocation formatting.
    /// </summary>
    /// <param name="message">
    ///     The <see cref="LogEntry"/> containing log data to be formatted.
    /// </param>
    /// <returns>
    ///     A formatted string including timestamp, optional event ID, log level, message, and exception details.
    ///     The format is: <c>[timestamp] [EventId] [Level] Message - Exception: ...</c>
    /// </returns>
    /// <example>
    /// <code>
    /// var formatter = new FileLogFormatter();
    /// var log = formatter.Format(
    ///     new LogEntry(
    ///         DateTime.UtcNow,
    ///         LogLevel.Information,
    ///         new EventId(100, "Startup"),
    ///         "Application started",
    ///         null));
    /// // Output: [2026-03-30 18:20:15.390] [100] [INFO] Application started
    /// </code>
    /// </example>
    public string Format(LogEntry message)
    {
        string timestamp = TimestampCache.GetFormattedTimestamp(message.Timestamp, TimestampFormat);

        bool hasEventId = message.EventId != EventId.Empty;
        bool hasException = message.Exception is not null;

        return string.Create(
            timestamp.Length + message.Message.Length + 64,
            (message, timestamp, hasEventId, hasException),
            (span, state) =>
            {
                (LogEntry entry, string? ts, bool eventIdFlag, bool exFlag) = state;

                int pos = 0;

                // [timestamp]
                span[pos++] = '[';
                ts.AsSpan().CopyTo(span[pos..]);
                pos += ts.Length;
                span[pos++] = ']';
                span[pos++] = ' ';

                // [EventId]
                if (eventIdFlag)
                {
                    span[pos++] = '[';
                    _ = entry.EventId.Id.TryFormat(span[pos..], out int written, provider: CultureInfo.InvariantCulture);
                    pos += written;
                    span[pos++] = ']';
                    span[pos++] = ' ';
                }

                // [LogLevel]
                span[pos++] = '[';
                ReadOnlySpan<char> levelSpan = LogLevelShortNames.GetShortName(entry.LogLevel);
                levelSpan.CopyTo(span[pos..]);
                pos += levelSpan.Length;
                span[pos++] = ']';
                span[pos++] = ' ';

                // Message
                entry.Message.AsSpan().CopyTo(span[pos..]);
                pos += entry.Message.Length;

                // Exception
                if (exFlag && entry.Exception is not null)
                {
                    const string prefix = " - Exception: ";
                    prefix.AsSpan().CopyTo(span[pos..]);
                    pos += prefix.Length;

                    string exStr = entry.Exception.ToString();
                    exStr.AsSpan().CopyTo(span[pos..]);
                }
            });
    }

    /// <inheritdoc/>
    public void Format(LogEntry message, StringBuilder sb)
    {
        ArgumentNullException.ThrowIfNull(sb);

        bool hasEventId = message.EventId != EventId.Empty;
        bool hasException = message.Exception is not null;

        // [timestamp]
        _ = sb.Append('[')
              .Append(TimestampCache.GetFormattedTimestamp(message.Timestamp, TimestampFormat))
              .Append(']')
              .Append(' ');

        // [EventId]
        if (hasEventId)
        {
            _ = sb.Append('[')
                  .Append(message.EventId.Id)
                  .Append(']')
                  .Append(' ');
        }

        // [LogLevel]
        _ = sb.Append('[')
              .Append(LogLevelShortNames
              .GetShortName(message.LogLevel))
              .Append(']')
              .Append(' ');

        // Message
        _ = sb.Append(message.Message);

        // Exception
        if (hasException && message.Exception is not null)
        {
            _ = sb.Append(" - Exception: ")
                  .Append(message.Exception);
        }
    }
}
