// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Logging.Models;

namespace Nalix.Logging.Formatters;

/// <summary>
/// Provides high-performance formatting of logging levels with zero allocations.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
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
        'F', 'A', 'I', 'L', '\0', // LogLevel.ERROR       (5)
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
        5, // ERROR (5)
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
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static System.ReadOnlySpan<System.Char> GetShortLogLevel(LogLevel logLevel)
    {
        // Bounds checking with bitwise operation for performance
        if (!((System.Byte)logLevel < MaxLogLevels))
        {
            // Fall back to the string representation for unknown levels
            return System.MemoryExtensions.AsSpan(logLevel.ToString().ToUpperInvariant());
        }

        // Get the pre-computed span for this log level
        return LogLevelChars.Slice(
            (System.Byte)logLevel * LogLevelPaddedLength,
            LogLevelLength
        );
    }
}
