using Notio.Common.Logging;
using System;
using System.Runtime.CompilerServices;

namespace Notio.Logging.Formatters;

/// <summary>
/// Provides high-performance formatting of logging levels with zero allocations.
/// </summary>
internal static class LoggingLevelFormatter
{
    // Constants for optimized memory layout
    private const int MaxLogLevels = 8;
    private const int LogLevelLength = 4;
    private const int LogLevelPaddedLength = 5; // 4 chars + null terminator

    // Character buffer is organized as fixed-length segments with null terminators
    // This enables fast slicing without calculating offsets each time
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

    // Pre-computed strings for each log level to avoid repeated allocations
    private static readonly string[] CachedLogLevels = new string[MaxLogLevels];

    // Color cache for console output
    private static readonly string[] ColorCodes = new string[MaxLogLevels];

    // Format masks for various output types
    private static readonly byte[] LevelSeverity =
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
        Span<char> buffer = stackalloc char[LogLevelLength];
        for (var i = 0; i < MaxLogLevels; i++)
        {
            // Extract the characters for this log level
            LogLevelChars.Slice(i * LogLevelPaddedLength, LogLevelLength).CopyTo(buffer);

            // Create and cache the string
            CachedLogLevels[i] = new string(buffer);
        }

        // Initialize color codes for console output
        ColorCodes[0] = "\u001b[36m"; // Meta - Cyan
        ColorCodes[1] = "\u001b[90m"; // Trace - Dark Gray
        ColorCodes[2] = "\u001b[37m"; // Debug - White
        ColorCodes[3] = "\u001b[32m"; // Information - Green
        ColorCodes[4] = "\u001b[33m"; // Warning - Yellow
        ColorCodes[5] = "\u001b[31m"; // Error - Red
        ColorCodes[6] = "\u001b[35m"; // Critical - Magenta
        ColorCodes[7] = "\u001b[37m"; // None - White
    }

    /// <summary>
    /// Gets a character span representation of the log level without allocating a string.
    /// </summary>
    /// <param name="logLevel">The logging level.</param>
    /// <returns>A character span representing the log level.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ReadOnlySpan<char> GetShortLogLevel(LoggingLevel logLevel)
    {
        // Bounds checking with bitwise operation for performance
        if (!IsValidLogLevel(logLevel))
        {
            // Fall back to the string representation for unknown levels
            return logLevel.ToString().ToUpperInvariant().AsSpan();
        }

        // Get the pre-computed span for this log level
        return LogLevelChars.Slice(
            (int)logLevel * LogLevelPaddedLength,
            LogLevelLength
        );
    }

    /// <summary>
    /// Gets a string representation of the log level using cached strings.
    /// </summary>
    /// <param name="logLevel">The logging level.</param>
    /// <returns>A string representing the log level.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string GetShortLogLevelString(LoggingLevel logLevel)
    {
        // Bounds checking with bitwise operation for performance
        if (!IsValidLogLevel(logLevel))
        {
            // Fall back to the string representation for unknown levels
            return logLevel.ToString().ToUpperInvariant();
        }

        // Return the cached string for this log level
        return CachedLogLevels[(int)logLevel];
    }

    /// <summary>
    /// Gets the ANSI color code for a log level.
    /// </summary>
    /// <param name="logLevel">The logging level.</param>
    /// <returns>A string containing the ANSI color code.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string GetColorCode(LoggingLevel logLevel)
    {
        if (!IsValidLogLevel(logLevel))
            return "\u001b[37m"; // Default to white

        return ColorCodes[(int)logLevel];
    }

    /// <summary>
    /// Gets the severity value of a log level (0-7).
    /// </summary>
    /// <param name="logLevel">The logging level.</param>
    /// <returns>A byte representing the severity (higher = more severe).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static byte GetSeverity(LoggingLevel logLevel)
    {
        if (!IsValidLogLevel(logLevel))
            return 0;

        return LevelSeverity[(int)logLevel];
    }

    /// <summary>
    /// Validates if a log level is within the expected range.
    /// </summary>
    /// <param name="logLevel">The logging level to validate.</param>
    /// <returns>True if the log level is valid, otherwise false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsValidLogLevel(LoggingLevel logLevel)
    {
        // Use unsigned comparison to handle both negative values and values larger than MaxLogLevels
        return (uint)logLevel < MaxLogLevels;
    }

    /// <summary>
    /// Directly copies the log level text into a destination buffer.
    /// </summary>
    /// <param name="logLevel">The logging level.</param>
    /// <param name="destination">The destination character span.</param>
    /// <returns>The number of characters written.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int CopyTo(LoggingLevel logLevel, Span<char> destination)
    {
        if (!IsValidLogLevel(logLevel))
        {
            string fallback = logLevel.ToString().ToUpperInvariant();
            if (fallback.Length > destination.Length)
                return 0;

            fallback.AsSpan().CopyTo(destination);
            return fallback.Length;
        }

        ReadOnlySpan<char> source = LogLevelChars.Slice(
            (int)logLevel * LogLevelPaddedLength,
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string GetFixedWidthLogLevel(LoggingLevel logLevel)
    {
        if (!IsValidLogLevel(logLevel))
        {
            // Truncate or pad to exactly 4 characters for consistency
            string fallback = logLevel.ToString().ToUpperInvariant();
            if (fallback.Length > 4)
                return fallback[..4];
            if (fallback.Length < 4)
                return fallback.PadRight(4);
            return fallback;
        }

        return CachedLogLevels[(int)logLevel];
    }

    /// <summary>
    /// Tries to parse a log level from its string representation.
    /// </summary>
    /// <param name="levelText">The text representation of a log level.</param>
    /// <param name="result">The parsed log level when successful.</param>
    /// <returns>True if parsing was successful, otherwise false.</returns>
    internal static bool TryParse(ReadOnlySpan<char> levelText, out LoggingLevel result)
    {
        result = LoggingLevel.None;

        if (levelText.IsEmpty)
            return false;

        // First check our cached short names for efficiency
        for (int i = 0; i < MaxLogLevels; i++)
        {
            ReadOnlySpan<char> candidate = LogLevelChars.Slice(i * LogLevelPaddedLength, LogLevelLength);
            if (levelText.Equals(candidate, StringComparison.OrdinalIgnoreCase))
            {
                result = (LoggingLevel)i;
                return true;
            }
        }

        // Handle full names and custom parsing
        if (levelText.Equals("META", StringComparison.OrdinalIgnoreCase) ||
            levelText.Equals("METADATA", StringComparison.OrdinalIgnoreCase))
        {
            result = LoggingLevel.Meta;
            return true;
        }

        if (levelText.Equals("TRACE", StringComparison.OrdinalIgnoreCase) ||
            levelText.Equals("VERBOSE", StringComparison.OrdinalIgnoreCase))
        {
            result = LoggingLevel.Trace;
            return true;
        }

        if (levelText.Equals("DEBUG", StringComparison.OrdinalIgnoreCase) ||
            levelText.Equals("DBUG", StringComparison.OrdinalIgnoreCase))
        {
            result = LoggingLevel.Debug;
            return true;
        }

        if (levelText.Equals("INFO", StringComparison.OrdinalIgnoreCase) ||
            levelText.Equals("INFORMATION", StringComparison.OrdinalIgnoreCase))
        {
            result = LoggingLevel.Information;
            return true;
        }

        if (levelText.Equals("WARN", StringComparison.OrdinalIgnoreCase) ||
            levelText.Equals("WARNING", StringComparison.OrdinalIgnoreCase))
        {
            result = LoggingLevel.Warning;
            return true;
        }

        if (levelText.Equals("ERROR", StringComparison.OrdinalIgnoreCase) ||
            levelText.Equals("FAIL", StringComparison.OrdinalIgnoreCase) ||
            levelText.Equals("ERR", StringComparison.OrdinalIgnoreCase))
        {
            result = LoggingLevel.Error;
            return true;
        }

        if (levelText.Equals("CRIT", StringComparison.OrdinalIgnoreCase) ||
            levelText.Equals("CRITICAL", StringComparison.OrdinalIgnoreCase) ||
            levelText.Equals("FATAL", StringComparison.OrdinalIgnoreCase))
        {
            result = LoggingLevel.Critical;
            return true;
        }

        if (levelText.Equals("NONE", StringComparison.OrdinalIgnoreCase) ||
            levelText.Equals("OFF", StringComparison.OrdinalIgnoreCase))
        {
            result = LoggingLevel.None;
            return true;
        }

        // Try numeric parsing as fallback
        if (int.TryParse(levelText, out int numericLevel) && numericLevel >= 0 && numericLevel < MaxLogLevels)
        {
            result = (LoggingLevel)numericLevel;
            return true;
        }

        return false;
    }
}
