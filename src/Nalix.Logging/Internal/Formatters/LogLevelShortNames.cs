// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Diagnostics;


#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Logging.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Logging.Benchmarks")]
#endif

namespace Nalix.Logging.Internal.Formatters;

/// <summary>
/// Provides high-performance formatting of logging levels with zero allocations.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
internal static class LogLevelShortNames
{
    #region Constants

    // Constants for optimized memory layout
    private const System.Int32 MaxLogLevels = 8;

    private const System.Int32 LogLevelLength = 4;
    private const System.Int32 LogLevelPaddedLength = 5; // 4 chars + null terminator

    #endregion Constants

    #region Fields

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

    #endregion Fields

    #region APIs

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.ReadOnlySpan<System.Char> GetShortName(LogLevel logLevel)
    {
        // Bounds checking with bitwise operation for performance
        if ((System.Byte)logLevel >= MaxLogLevels)
        {
            // Fall back to the string representation for unknown levels
            return System.MemoryExtensions.AsSpan(logLevel.ToString().ToUpperInvariant());
        }

        // Get the pre-computed span for this log level
        return LogLevelChars.Slice((System.Byte)logLevel * LogLevelPaddedLength, LogLevelLength);
    }

    #endregion APIs
}
