// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Nalix.Common.Diagnostics;
using Nalix.Logging.Internal.Formatters;
using Nalix.Logging.Internal.Pooling;

namespace Nalix.Logging.Formatters;

/// <summary>
/// Logging Formatter chuyên dụng cho xuất có màu ANSI.
/// </summary>
[DebuggerDisplay("AnsiColorFormatter")]
[ExcludeFromCodeCoverage]
internal class AnsiColorFormatter : ILoggerFormatter
{
    private const string TimestampFormat = "HH:mm:ss.fff";

    /// <inheritdoc/>
    public string Format(LogEntry message)
    {
        string timestamp = TimestampCache.GetFormattedTimestamp(message.Timestamp, TimestampFormat);
        string levelColor = AnsiColors.GetForLevel(message.LogLevel);

        Exception? ex = message.Exception;
        bool hasException = message.Exception is not null;
        bool hasEventId = message.EventId != EventId.Empty;

        int exceptionLen = 0;
        if (ex != null)
        {
            // Lấy tên loại exception & nội dung message an toàn
            exceptionLen = (AnsiColors.Red.Length
                         + 13 // " - Exception: "
                         + ex.GetType().Name.Length
                         + ex.Message?.Length) ?? (0
                         + (ex.ToString()?.Length ?? 0)
                         + AnsiColors.White.Length);
        }

        // Tính toán độ dài tối thiểu
        int len =
            AnsiColors.White.Length + 1 + timestamp.Length + 1 + // [timestamp] với màu
            AnsiColors.White.Length + 1 + 4 + 1 +                 // [LVL] với màu
            (hasEventId ? AnsiColors.Cyan.Length + 1 + 5 + 1 + (message.EventId.Name?.Length ?? 0) + 1 + AnsiColors.DarkGray.Length : 0) + // [EventId]
            message.Message.Length + 3 +                        // Message + " - "
            exceptionLen
            + 32; // buffer dư

        return string.Create(len, (message, timestamp, levelColor, hasEventId, hasException), (span, state) =>
        {
            (LogEntry entry, string? ts, string? color, bool evt, bool ex) = state;
            int pos = 0;

            // [timestamp]
            AnsiColors.White.AsSpan().CopyTo(span[pos..]); pos += AnsiColors.White.Length;
            span[pos++] = '[';
            ts.AsSpan().CopyTo(span[pos..]); pos += ts.Length;
            span[pos++] = ']';

            // Log Level
            span[pos++] = ' ';
            color.AsSpan().CopyTo(span[pos..]); pos += color.Length;
            span[pos++] = '[';
            ReadOnlySpan<char> shortLvl = LogLevelShortNames.GetShortName(entry.LogLevel);
            shortLvl.CopyTo(span[pos..]); pos += shortLvl.Length;
            span[pos++] = ']';
            AnsiColors.White.AsSpan().CopyTo(span[pos..]); pos += AnsiColors.White.Length;

            // EventId
            if (evt)
            {
                span[pos++] = ' ';

                AnsiColors.Cyan.AsSpan().CopyTo(span[pos..]); pos += AnsiColors.Cyan.Length;
                span[pos++] = '[';
                entry.EventId.Id.TryFormat(span[pos..], out int written, provider: CultureInfo.InvariantCulture);
                pos += written;

                if (!string.IsNullOrEmpty(entry.EventId.Name))
                {
                    span[pos++] = ':';
                    AnsiColors.DarkGray.AsSpan().CopyTo(span[pos..]); pos += AnsiColors.DarkGray.Length;
                    entry.EventId.Name.AsSpan().CopyTo(span[pos..]); pos += entry.EventId.Name.Length;
                }
                span[pos++] = ']';
                AnsiColors.White.AsSpan().CopyTo(span[pos..]); pos += AnsiColors.White.Length;
            }

            // Message
            span[pos++] = ' ';
            span[pos++] = '-';
            span[pos++] = ' ';
            entry.Message.AsSpan().CopyTo(span[pos..]); pos += entry.Message.Length;

            // Exception
            if (ex && entry.Exception is not null)
            {
                AnsiColors.Red.AsSpan().CopyTo(span[pos..]); pos += AnsiColors.Red.Length;
                const string excPrefix = " - Exception: ";
                excPrefix.AsSpan().CopyTo(span[pos..]); pos += excPrefix.Length;
                string exStr = entry.Exception.ToString() ?? "";
                exStr.AsSpan().CopyTo(span[pos..]); pos += exStr.Length;
                AnsiColors.White.AsSpan().CopyTo(span[pos..]);
            }
        });
    }

    /// <inheritdoc/>
    public void Format(LogEntry message, StringBuilder sb)
    {
        ArgumentNullException.ThrowIfNull(sb);

        _ = sb.Append(AnsiColors.White)
              .Append('[')
              .Append(AnsiColors.Blue)
              .Append(TimestampCache.GetFormattedTimestamp(message.Timestamp, TimestampFormat))
              .Append(AnsiColors.White)
              .Append(']')
              .Append(' ')
              .Append(AnsiColors.GetForLevel(message.LogLevel))
              .Append('[').Append(LogLevelShortNames.GetShortName(message.LogLevel)).Append(']')
              .Append(AnsiColors.White);

        if (message.EventId != EventId.Empty)
        {
            _ = sb.Append(' ')
                  .Append(AnsiColors.Cyan).Append('[').Append(message.EventId.Id);

            if (!string.IsNullOrEmpty(message.EventId.Name))
            {
                _ = sb.Append(':')
                      .Append(AnsiColors.DarkGray).Append(message.EventId.Name);
            }
            _ = sb.Append(']')
                  .Append(AnsiColors.White);
        }

        _ = sb.Append(AnsiColors.DarkGray)
              .Append(" - ")
              .Append(AnsiColors.White)
              .Append(message.Message);

        if (message.Exception is not null)
        {
            _ = sb.Append(AnsiColors.Red)
                  .Append(" - Exception: ")
                  .Append(message.Exception)
                  .Append(AnsiColors.White);
        }
    }
}
