// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Nalix.Logging.Internal.Formatters;
using Nalix.Logging.Internal.Pooling;

namespace Nalix.Logging.Formatters;

/// <summary>
/// Logging Formatter chuyên dụng cho xuất có màu ANSI.
/// </summary>
[DebuggerDisplay("AnsiColorFormatter")]
[ExcludeFromCodeCoverage]
internal class AnsiColorFormatter : INLogixFormatter
{
    private const string TimestampFormat = "HH:mm:ss.fff";

    /// <inheritdoc/>
    public string Format(
        DateTime timestampUtc,
        LogLevel logLevel,
        EventId eventId,
        string message,
        Exception? exception)
    {
        string timestamp = TimestampCache.GetFormattedTimestamp(timestampUtc, TimestampFormat);
        string levelColor = AnsiColors.GetForLevel(logLevel);

        int exceptionLen = exception is null
            ? 0
            : AnsiColors.Red.Length + 13 + (exception.ToString()?.Length ?? 0) + AnsiColors.White.Length;

        bool hasEventId = eventId != default;
        int len =
            AnsiColors.White.Length + 1 + timestamp.Length + 1 +
            AnsiColors.White.Length + 1 + 4 + 1 +
            (hasEventId ? AnsiColors.Cyan.Length + 1 + 5 + 1 + (eventId.Name?.Length ?? 0) + 1 + AnsiColors.DarkGray.Length : 0) +
            message.Length + 3 +
            exceptionLen +
            32;

        return string.Create(
            len,
            (timestamp, levelColor, hasEventId, logLevel, eventId, message, exception),
            (span, state) =>
            {
                (string ts, string color, bool hasEvt, LogLevel level, EventId evt, string msg, Exception? ex) = state;
                int pos = 0;

                AnsiColors.White.AsSpan().CopyTo(span[pos..]); pos += AnsiColors.White.Length;
                span[pos++] = '[';
                ts.AsSpan().CopyTo(span[pos..]); pos += ts.Length;
                span[pos++] = ']';

                span[pos++] = ' ';
                color.AsSpan().CopyTo(span[pos..]); pos += color.Length;
                span[pos++] = '[';
                ReadOnlySpan<char> shortLvl = LogLevelShortNames.GetShortName(level);
                shortLvl.CopyTo(span[pos..]); pos += shortLvl.Length;
                span[pos++] = ']';
                AnsiColors.White.AsSpan().CopyTo(span[pos..]); pos += AnsiColors.White.Length;

                if (hasEvt)
                {
                    span[pos++] = ' ';
                    AnsiColors.Cyan.AsSpan().CopyTo(span[pos..]); pos += AnsiColors.Cyan.Length;
                    span[pos++] = '[';
                    _ = evt.Id.TryFormat(span[pos..], out int written, provider: CultureInfo.InvariantCulture);
                    pos += written;

                    if (!string.IsNullOrEmpty(evt.Name))
                    {
                        span[pos++] = ':';
                        AnsiColors.DarkGray.AsSpan().CopyTo(span[pos..]); pos += AnsiColors.DarkGray.Length;
                        evt.Name.AsSpan().CopyTo(span[pos..]); pos += evt.Name.Length;
                    }

                    span[pos++] = ']';
                    AnsiColors.White.AsSpan().CopyTo(span[pos..]); pos += AnsiColors.White.Length;
                }

                span[pos++] = ' ';
                span[pos++] = '-';
                span[pos++] = ' ';
                msg.AsSpan().CopyTo(span[pos..]); pos += msg.Length;

                if (ex is not null)
                {
                    AnsiColors.Red.AsSpan().CopyTo(span[pos..]); pos += AnsiColors.Red.Length;
                    const string excPrefix = " - Exception: ";
                    excPrefix.AsSpan().CopyTo(span[pos..]); pos += excPrefix.Length;
                    string exStr = ex.ToString() ?? "";
                    exStr.AsSpan().CopyTo(span[pos..]); pos += exStr.Length;
                    AnsiColors.White.AsSpan().CopyTo(span[pos..]);
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
        ArgumentNullException.ThrowIfNull(sb);

        _ = sb.Append(AnsiColors.White)
              .Append('[')
              .Append(AnsiColors.Blue)
              .Append(TimestampCache.GetFormattedTimestamp(timestampUtc, TimestampFormat))
              .Append(AnsiColors.White)
              .Append(']')
              .Append(' ')
              .Append(AnsiColors.GetForLevel(logLevel))
              .Append('[').Append(LogLevelShortNames.GetShortName(logLevel)).Append(']')
              .Append(AnsiColors.White);

        if (eventId != default)
        {
            _ = sb.Append(' ')
                  .Append(AnsiColors.Cyan).Append('[').Append(eventId.Id);

            if (!string.IsNullOrEmpty(eventId.Name))
            {
                _ = sb.Append(':')
                      .Append(AnsiColors.DarkGray).Append(eventId.Name);
            }

            _ = sb.Append(']')
                  .Append(AnsiColors.White);
        }

        _ = sb.Append(AnsiColors.DarkGray)
              .Append(" - ")
              .Append(AnsiColors.White)
              .Append(message);

        if (exception is not null)
        {
            _ = sb.Append(AnsiColors.Red)
                  .Append(" - Exception: ")
                  .Append(exception)
                  .Append(AnsiColors.White);
        }
    }
}
