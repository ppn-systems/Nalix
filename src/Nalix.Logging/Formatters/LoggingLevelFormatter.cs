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
        'N', 'O', 'N', 'E', '\0'  // LogLevel.NONE        (7)
    ];

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static System.Int32 MapIndex(LogLevel level) => level switch
    {
        LogLevel.Meta => 0,
        LogLevel.Trace => 1,
        LogLevel.Debug => 2,
        LogLevel.Information => 3,
        LogLevel.Warning => 4,
        LogLevel.Error => 5,
        LogLevel.Critical => 6,
        LogLevel.None => 7,
        _ => -1
    };

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal static System.ReadOnlySpan<System.Char> GetShortLogLevel(LogLevel logLevel)
    {
        System.Int32 idx = MapIndex(logLevel);
        return (System.UInt32)idx >= MaxLogLevels
            ? System.MemoryExtensions.AsSpan(logLevel.ToString().ToUpperInvariant())
            : LogLevelChars.Slice(idx * LogLevelPaddedLength, LogLevelLength);
    }
}
