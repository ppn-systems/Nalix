using Notio.Common.Enums;
using Notio.Common.Models;
using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;

namespace Notio.Logging.Formatters
{
    internal static class LoggingBuilder
    {
        private const int DefaultBufferSize = 60;
        private const char OpenBracket = '[';
        private const char CloseBracket = ']';
        private const char DefaultSpaceSeparator = ' ';
        private const char DefaultDashSeparator = '-';
        private static readonly ArrayPool<char> CharPool = ArrayPool<char>.Shared;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void BuildLog(
            StringBuilder builder,
            in DateTime timeStamp,
            LoggingLevel logLevel,
            in EventId eventId,
            string message,
            Exception? exception,
            bool useColor = false,
            string? customTimestampFormat = null)
        {
            // Estimate buffer size to minimize reallocations
            int estimatedLength = CalculateEstimatedLength(message, eventId, exception, useColor);
            EnsureCapacity(builder, estimatedLength);

            AppendTimestamp(builder, timeStamp, customTimestampFormat, useColor);
            AppendEventId(builder, eventId, useColor);
            AppendLogLevel(builder, logLevel, useColor);
            AppendMessage(builder, message, useColor);
            AppendException(builder, exception, useColor);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AppendTimestamp(StringBuilder builder, in DateTime timeStamp, string? format, bool useColor)
        {
            string timestampFormat = format ?? "yyyy-MM-dd HH:mm:ss.fff";
            Span<char> dateBuffer = stackalloc char[timestampFormat.Length + 10];

            if (timeStamp.TryFormat(dateBuffer, out int charsWritten, timestampFormat))
            {
                if (useColor)
                {
                    builder.Append(OpenBracket)
                           .Append(ColorAnsi.Blue)
                           .Append(dateBuffer[..charsWritten])
                           .Append(ColorAnsi.White)
                           .Append(CloseBracket);
                }
                else
                {
                    builder.Append(OpenBracket)
                           .Append(dateBuffer[..charsWritten])
                           .Append(CloseBracket);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AppendLogLevel(StringBuilder builder, LoggingLevel logLevel, bool useColor)
        {
            if (useColor)
            {
                builder.Append(DefaultSpaceSeparator)
                       .Append(OpenBracket)
                       .Append(ColorAnsi.GetColorCode(logLevel))
                       .Append(LoggingLevelFormatter.GetShortLogLevelString(logLevel))
                       .Append(ColorAnsi.White)
                       .Append(CloseBracket);
            }
            else
            {
                builder.Append(DefaultSpaceSeparator)
                       .Append(OpenBracket)
                       .Append(LoggingLevelFormatter.GetShortLogLevel(logLevel))
                       .Append(CloseBracket);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AppendEventId(StringBuilder builder, in EventId eventId, bool useColor)
        {
            if (eventId.Id == 0) return;

            builder.Append(DefaultSpaceSeparator)
                   .Append(OpenBracket);

            if (eventId.Name is not null)
            {
                if (useColor)
                {
                    builder.Append(ColorAnsi.Blue)
                           .Append(eventId.Id)
                           .Append(':')
                           .Append(eventId.Name)
                           .Append(ColorAnsi.White);
                }
                else
                {
                    builder.Append(eventId.Id)
                           .Append(':')
                           .Append(eventId.Name);
                }
            }
            else
            {
                builder.Append(eventId.Id);
            }

            builder.Append(CloseBracket);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AppendMessage(StringBuilder builder, string message, bool useColor)
        {
            builder.Append(DefaultSpaceSeparator)
                   .Append(DefaultDashSeparator)
                   .Append(DefaultSpaceSeparator);

            if (useColor)
            {
                builder.Append(ColorAnsi.DarkGray)
                       .Append(message)
                       .Append(ColorAnsi.White);
            }
            else
            {
                builder.Append(message);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AppendException(StringBuilder builder, Exception? exception, bool useColor)
        {
            if (exception is null) return;

            builder.Append(DefaultSpaceSeparator)
                   .Append(DefaultDashSeparator)
                   .Append(DefaultSpaceSeparator)
                   .AppendLine();

            if (useColor)
            {
                builder.Append(ColorAnsi.Red)
                       .Append(exception)
                       .Append(ColorAnsi.White);
            }
            else
            {
                builder.Append(exception);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CalculateEstimatedLength(string message, in EventId eventId, Exception? exception, bool useColor)
        {
            int length = message.Length;

            if (eventId.Name is not null)
                length += eventId.Name.Length + 4; // Bao gồm ':' và dấu ngoặc vuông

            if (exception is not null)
                length += exception.ToString().Length + 2;

            if (useColor) length += 30;

            return length + DefaultBufferSize;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void EnsureCapacity(StringBuilder builder, int capacity)
        {
            if (builder.Capacity < capacity)
                builder.EnsureCapacity(capacity);
        }
    }
}