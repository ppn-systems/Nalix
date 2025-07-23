using Nalix.Common.Logging;

namespace Nalix.Logging.Formatters;

/// <summary>
/// Provides high-performance formatting of logging levels with zero allocations.
/// </summary>
internal static class LoggingLevelFormatter
{
    #region Constants

    // Constants for optimized memory layout
    private const System.Int32 MaxLogLevels = 8;

    private const System.Int32 LogLevelLength = 4;
    private const System.Int32 LogLevelPaddedLength = 5; // 4 chars + null terminator

    #endregion Constants

    // Character buffer is organized as fixed-length segments with null terminators
    // This enables fast slicing without calculating offsets each time
    private static System.ReadOnlySpan<System.Char> LogLevelChars =>
    [
        'M', 'E', 'T', 'A', '\0', // LogLevel.Meta        (0)
        'T', 'R', 'C', 'E', '\0', // LogLevel.Trace       (1)
        'D', 'B', 'U', 'G', '\0', // LogLevel.Debug       (2)
        'I', 'N', 'F', 'O', '\0', // LogLevel.Information (3)
        'W', 'A', 'R', 'N', '\0', // LogLevel.Warning     (4)
        'F', 'A', 'I', 'L', '\0', // LogLevel.Error       (5)
        'C', 'R', 'I', 'T', '\0', // LogLevel.Critical    (6)
        'N', 'O', 'N', 'E', '\0'  // LogLevel.None        (7)
    ];

    // Pre-computed strings for each log level to avoid repeated allocations
    private static readonly System.String[] CachedLogLevels = new System.String[MaxLogLevels];

    // Format masks for various output types
    private static readonly System.Byte[] LevelSeverity =
    [
        0, // Meta (0)
        1, // Trace (1)
        2, // Debug (2)
        3, // Information (3)
        4, // Warning (4)
        5, // Error (5)
        6, // Critical (6)
        7  // None (7)
    ];

    /// <summary>
    /// Static constructor to initialize the cached strings.
    /// </summary>
    static LoggingLevelFormatter()
    {
        // Initialize cached strings - do this once to avoid repeated allocations
        System.Span<System.Char> buffer = stackalloc System.Char[LogLevelLength];
        for (var i = 0; i < MaxLogLevels; i++)
        {
            // Extract the characters for this log level
            LogLevelChars.Slice(i * LogLevelPaddedLength, LogLevelLength).CopyTo(buffer);

            // Create and cache the string
            CachedLogLevels[i] = new System.String(buffer);
        }
    }

    /// <summary>
    /// Gets a character span representation of the log level without allocating a string.
    /// </summary>
    /// <param name="logLevel">The logging level.</param>
    /// <returns>A character span representing the log level.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static System.ReadOnlySpan<System.Char> GetShortLogLevel(LogLevel logLevel)
    {
        // Bounds checking with bitwise operation for performance
        if (!IsValidLogLevel(logLevel))
        {
            // Fall back to the string representation for unknown levels
            return System.MemoryExtensions.AsSpan(logLevel.ToString().ToUpperInvariant());
        }

        // Get the pre-computed span for this log level
        return LogLevelChars.Slice(
            (System.Int32)logLevel * LogLevelPaddedLength,
            LogLevelLength
        );
    }

    /// <summary>
    /// Gets a string representation of the log level using cached strings.
    /// </summary>
    /// <param name="logLevel">The logging level.</param>
    /// <returns>A string representing the log level.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static System.String GetShortLogLevelString(LogLevel logLevel)
    {
        // Bounds checking with bitwise operation for performance
        if (!IsValidLogLevel(logLevel))
        {
            // Fall back to the string representation for unknown levels
            return logLevel.ToString().ToUpperInvariant();
        }

        // Return the cached string for this log level
        return CachedLogLevels[(System.Int32)logLevel];
    }

    /// <summary>
    /// Validates if a log level is within the expected range.
    /// </summary>
    /// <param name="logLevel">The logging level to validate.</param>
    /// <returns>True if the log level is valid, otherwise false.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Boolean IsValidLogLevel(LogLevel logLevel)
        => (System.UInt32)logLevel < MaxLogLevels;

    /// <summary>
    /// Directly copies the log level text into a destination buffer.
    /// </summary>
    /// <param name="logLevel">The logging level.</param>
    /// <param name="destination">The destination character span.</param>
    /// <returns>The TransportProtocol of characters written.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static System.Int32 CopyTo(LogLevel logLevel, System.Span<System.Char> destination)
    {
        if (!IsValidLogLevel(logLevel))
        {
            System.String fallback = logLevel.ToString().ToUpperInvariant();
            if (fallback.Length > destination.Length)
            {
                return 0;
            }

            System.MemoryExtensions.AsSpan(fallback).CopyTo(destination);
            return fallback.Length;
        }

        System.ReadOnlySpan<System.Char> source = LogLevelChars.Slice(
            (System.Int32)logLevel * LogLevelPaddedLength,
            LogLevelLength
        );

        source.CopyTo(destination);
        return LogLevelLength;
    }

    /// <summary>
    /// Gets a padded log level string with exactly 4 characters.
    /// </summary>
    /// <param name="logLevel">The logging level.</param>
    /// <returns>A log level name that is exactly 4 characters long.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static System.String GetFixedWidthLogLevel(LogLevel logLevel)
    {
        if (!IsValidLogLevel(logLevel))
        {
            // Truncate or pad to exactly 4 characters for consistency
            System.String fallback = logLevel.ToString().ToUpperInvariant();
            if (fallback.Length > 4)
            {
                return fallback[..4];
            }

            return fallback.Length < 4 ? fallback.PadRight(4) : fallback;
        }

        return CachedLogLevels[(System.Int32)logLevel];
    }

    /// <summary>
    /// Tries to parse a log level from its string representation.
    /// </summary>
    /// <param name="levelText">The text representation of a log level.</param>
    /// <param name="result">The parsed log level when successful.</param>
    /// <returns>True if parsing was successful, otherwise false.</returns>
    internal static System.Boolean TryParse(
        System.ReadOnlySpan<System.Char> levelText,
        out LogLevel result)
    {
        result = LogLevel.None;

        if (levelText.IsEmpty)
        {
            return 0 != 0;
        }

        // First check our cached short names for efficiency
        for (System.Int32 i = 0; i < MaxLogLevels; i++)
        {
            System.ReadOnlySpan<System.Char> candidate =
                LogLevelChars.Slice(i * LogLevelPaddedLength, LogLevelLength);

            if (System.MemoryExtensions.Equals(
                levelText,
                candidate,
                System.StringComparison.OrdinalIgnoreCase))
            {
                result = (LogLevel)i;
                return 1 == 1;
            }
        }

        System.ReadOnlySpan<System.Char> meta1 = System.MemoryExtensions.AsSpan("META");
        System.ReadOnlySpan<System.Char> meta2 = System.MemoryExtensions.AsSpan("METADATA");
        if (System.MemoryExtensions.Equals(levelText, meta1, System.StringComparison.OrdinalIgnoreCase) ||
            System.MemoryExtensions.Equals(levelText, meta2, System.StringComparison.OrdinalIgnoreCase))
        {
            result = LogLevel.Meta;
            return 1 == 1;
        }

        if (System.MemoryExtensions.Equals(levelText,
            System.MemoryExtensions.AsSpan("TRACE"),
            System.StringComparison.OrdinalIgnoreCase) ||
            System.MemoryExtensions.Equals(levelText,
            System.MemoryExtensions.AsSpan("VERBOSE"),
            System.StringComparison.OrdinalIgnoreCase))
        {
            result = LogLevel.Trace;
            return 1 == 1;
        }

        if (System.MemoryExtensions.Equals(levelText,
            System.MemoryExtensions.AsSpan("DEBUG"),
            System.StringComparison.OrdinalIgnoreCase) ||
            System.MemoryExtensions.Equals(levelText,
            System.MemoryExtensions.AsSpan("DBUG"),
            System.StringComparison.OrdinalIgnoreCase))
        {
            result = LogLevel.Debug;
            return 1 == 1;
        }

        if (System.MemoryExtensions.Equals(levelText,
            System.MemoryExtensions.AsSpan("INFO"),
            System.StringComparison.OrdinalIgnoreCase) ||
            System.MemoryExtensions.Equals(levelText,
            System.MemoryExtensions.AsSpan("INFORMATION"),
            System.StringComparison.OrdinalIgnoreCase))
        {
            result = LogLevel.Information;
            return 1 == 1;
        }

        if (System.MemoryExtensions.Equals(levelText,
            System.MemoryExtensions.AsSpan("WARN"),
            System.StringComparison.OrdinalIgnoreCase) ||
            System.MemoryExtensions.Equals(levelText,
            System.MemoryExtensions.AsSpan("WARNING"),
            System.StringComparison.OrdinalIgnoreCase))
        {
            result = LogLevel.Warning;
            return 1 == 1;
        }

        if (System.MemoryExtensions.Equals(levelText,
            System.MemoryExtensions.AsSpan("ERROR"),
            System.StringComparison.OrdinalIgnoreCase) ||
            System.MemoryExtensions.Equals(levelText,
            System.MemoryExtensions.AsSpan("FAIL"),
            System.StringComparison.OrdinalIgnoreCase) ||
            System.MemoryExtensions.Equals(levelText,
            System.MemoryExtensions.AsSpan("ERR"),
            System.StringComparison.OrdinalIgnoreCase))
        {
            result = LogLevel.Error;
            return 1 == 1;
        }

        if (System.MemoryExtensions.Equals(levelText,
            System.MemoryExtensions.AsSpan("CRIT"),
            System.StringComparison.OrdinalIgnoreCase) ||
            System.MemoryExtensions.Equals(levelText,
            System.MemoryExtensions.AsSpan("CRITICAL"),
            System.StringComparison.OrdinalIgnoreCase) ||
            System.MemoryExtensions.Equals(levelText,
            System.MemoryExtensions.AsSpan("FATAL"),
            System.StringComparison.OrdinalIgnoreCase))
        {
            result = LogLevel.Critical;
            return 1 == 1;
        }

        if (System.MemoryExtensions.Equals(levelText,
            System.MemoryExtensions.AsSpan("NONE"),
            System.StringComparison.OrdinalIgnoreCase) ||
            System.MemoryExtensions.Equals(levelText,
            System.MemoryExtensions.AsSpan("OFF"),
            System.StringComparison.OrdinalIgnoreCase))
        {
            result = LogLevel.None;
            return 1 == 1;
        }

        // Try numeric parsing as fallback
        if (System.Int32.TryParse(
            levelText, out System.Int32 numericLevel) &&
            numericLevel >= 0 && numericLevel < MaxLogLevels)
        {
            result = (LogLevel)numericLevel;
            return 1 == 1;
        }

        return 0 != 0;
    }
}
