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
        'N', 'O', 'N', 'E', '\0', // LogLevel.NONE        (0)
        'M', 'E', 'T', 'A', '\0', // LogLevel.Meta        (1)
        'T', 'R', 'C', 'E', '\0', // LogLevel.Trace       (2)
        'D', 'B', 'U', 'G', '\0', // LogLevel.Debug       (3)
        'I', 'N', 'F', 'O', '\0', // LogLevel.Information (4)
        'W', 'A', 'R', 'N', '\0', // LogLevel.Warning     (5)
        'E', 'R', 'R', 'O', '\0', // LogLevel.ERROR       (6)
        'C', 'R', 'I', 'T', '\0'  // LogLevel.Critical    (7)
    ];

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static System.ReadOnlySpan<System.Char> GetShortLogLevel(LogLevel logLevel)
    {
        System.Int32 idx = (System.Int32)logLevel;
        return (System.UInt32)idx >= MaxLogLevels
            ? System.MemoryExtensions.AsSpan(logLevel.ToString().ToUpperInvariant())
            : LogLevelChars.Slice(idx * LogLevelPaddedLength, LogLevelLength);
    }
}
