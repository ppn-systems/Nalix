using Notio.Logging.Enums;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Notio.Logging.Format;

internal static class LoggingLevelFormatter
{
    private const int MAX_LOG_LEVELS = 8;
    private const int LOG_LEVEL_LENGTH = 4;

    // Sử dụng ReadOnlySpan trực tiếp thay vì array
    private static ReadOnlySpan<char> LOG_LEVEL_CHARS =>
    [
        'M', 'E', 'T', 'A', '\0', // LoggingLevel.Meta        (0)
        'T', 'R', 'C', 'E', '\0', // LoggingLevel.Trace       (1)
        'D', 'B', 'U', 'G', '\0', // LoggingLevel.Debug       (2)
        'I', 'N', 'F', 'O', '\0', // LoggingLevel.Information (3)
        'W', 'A', 'R', 'N', '\0', // LoggingLevel.Warning     (4)
        'F', 'A', 'I', 'L', '\0', // LoggingLevel.Error       (5)
        'C', 'R', 'I', 'T', '\0', // LoggingLevel.Critical    (6)
        'N', 'O', 'N', 'E', '\0'  // LoggingLevel.None        (7)
    ];

    // Cache các string phổ biến để tránh allocations
    private static readonly string[] CACHED_LOG_LEVELS = new string[MAX_LOG_LEVELS];

    // ThreadLocal buffer với kích thước cố định
    private static readonly ThreadLocal<char[]> LogLevelBuffer =
        new(() => GC.AllocateUninitializedArray<char>(LOG_LEVEL_LENGTH, pinned: true));

    static LoggingLevelFormatter()
    {
        // Pre-cache tất cả log level strings
        Span<char> buffer = stackalloc char[LOG_LEVEL_LENGTH];
        for (var i = 0; i < MAX_LOG_LEVELS; i++)
        {
            LOG_LEVEL_CHARS.Slice(i * (LOG_LEVEL_LENGTH + 1), LOG_LEVEL_LENGTH).CopyTo(buffer);
            CACHED_LOG_LEVELS[i] = new string(buffer);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool TryGetShortLogLevel(LoggingLevel logLevel, Span<char> destination)
    {
        if (!IsValidLogLevel(logLevel) || destination.Length < LOG_LEVEL_LENGTH)
            return false;

        ref char sourceRef = ref MemoryMarshal.GetReference(LOG_LEVEL_CHARS);
        ref char destRef = ref MemoryMarshal.GetReference(destination);

        var offset = (int)logLevel * (LOG_LEVEL_LENGTH + 1);
        Unsafe.CopyBlockUnaligned(
            ref Unsafe.As<char, byte>(ref destRef),
            ref Unsafe.As<char, byte>(ref Unsafe.Add(ref sourceRef, offset)),
            (uint)(LOG_LEVEL_LENGTH * sizeof(char))
        );

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ReadOnlySpan<char> GetShortLogLevel(LoggingLevel logLevel)
    {
        if (!IsValidLogLevel(logLevel))
            return logLevel.ToString().ToUpperInvariant().AsSpan();

        return LOG_LEVEL_CHARS.Slice(
            (int)logLevel * (LOG_LEVEL_LENGTH + 1),
            LOG_LEVEL_LENGTH
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string GetShortLogLevelString(LoggingLevel logLevel)
    {
        if (!IsValidLogLevel(logLevel))
            return logLevel.ToString().ToUpperInvariant();

        return CACHED_LOG_LEVELS[(int)logLevel];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsValidLogLevel(LoggingLevel logLevel) =>
        (uint)logLevel < MAX_LOG_LEVELS;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void AppendLogLevel(StringBuilder builder, LoggingLevel logLevel)
    {
        if (!IsValidLogLevel(logLevel))
        {
            builder.Append(logLevel.ToString().ToUpperInvariant());
            return;
        }

        // Sử dụng string đã cache
        builder.Append(CACHED_LOG_LEVELS[(int)logLevel]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool FormatLogLevel(LoggingLevel logLevel, Span<char> buffer, out int charsWritten)
    {
        if (!IsValidLogLevel(logLevel))
        {
            var str = logLevel.ToString().ToUpperInvariant();
            if (buffer.Length < str.Length)
            {
                charsWritten = 0;
                return false;
            }
            str.AsSpan().CopyTo(buffer);
            charsWritten = str.Length;
            return true;
        }

        if (buffer.Length < LOG_LEVEL_LENGTH)
        {
            charsWritten = 0;
            return false;
        }

        ref char sourceRef = ref MemoryMarshal.GetReference(LOG_LEVEL_CHARS);
        ref char destRef = ref MemoryMarshal.GetReference(buffer);

        var offset = (int)logLevel * (LOG_LEVEL_LENGTH + 1);
        Unsafe.CopyBlockUnaligned(
            ref Unsafe.As<char, byte>(ref destRef),
            ref Unsafe.As<char, byte>(ref Unsafe.Add(ref sourceRef, offset)),
            (uint)(LOG_LEVEL_LENGTH * sizeof(char))
        );

        charsWritten = LOG_LEVEL_LENGTH;
        return true;
    }
}