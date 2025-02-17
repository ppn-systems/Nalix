using Notio.Common.Enums;
using System;
using System.Runtime.CompilerServices;

namespace Notio.Logging.Formatters;

internal static class LoggingLevelFormatter
{
    private const int MaxLogLevels = 8;
    private const int LogLevelLength = 4;

    // Sử dụng ReadOnlySpan trực tiếp thay vì array
    private static ReadOnlySpan<char> LogLevelChars =>
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
    private static readonly string[] CachedLogLevels = new string[MaxLogLevels];

    static LoggingLevelFormatter()
    {
        Span<char> buffer = stackalloc char[LogLevelLength];
        for (var i = 0; i < MaxLogLevels; i++)
        {
            LogLevelChars.Slice(i * (LogLevelLength + 1), LogLevelLength).CopyTo(buffer);
            CachedLogLevels[i] = new string(buffer);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ReadOnlySpan<char> GetShortLogLevel(LoggingLevel logLevel)
    {
        if (!IsValidLogLevel(logLevel))
            return logLevel.ToString().ToUpperInvariant().AsSpan();

        return LogLevelChars.Slice(
            (int)logLevel * (LogLevelLength + 1),
            LogLevelLength
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string GetShortLogLevelString(LoggingLevel logLevel)
    {
        if (!IsValidLogLevel(logLevel))
            return logLevel.ToString().ToUpperInvariant();

        return CachedLogLevels[(int)logLevel];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsValidLogLevel(LoggingLevel logLevel) => (uint)logLevel < MaxLogLevels;
}
