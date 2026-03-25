// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Nalix.Common.Diagnostics;

#if DEBUG
[assembly: InternalsVisibleTo("Nalix.Logging.Tests")]
[assembly: InternalsVisibleTo("Nalix.Logging.Benchmarks")]
#endif

namespace Nalix.Logging.Internal.Formatters;

/// <summary>
/// Provides high-performance formatting of logging levels with zero allocations.
/// </summary>
[DebuggerNonUserCode]
[ExcludeFromCodeCoverage]
internal static class LogLevelShortNames
{
    #region Constants

    /// <summary>
    /// Constants for optimized memory layout
    /// </summary>
    private const int MaxLogLevels = 8;

    private const int LogLevelLength = 4;
    /// <summary>
    /// 4 chars + null terminator
    /// </summary>
    private const int LogLevelPaddedLength = 5;

    #endregion Constants

    #region Fields

    /// <summary>
    /// Character buffer is organized as fixed-length segments with null terminators
    /// This enables fast slicing without calculating offsets each time
    /// </summary>
    private static ReadOnlySpan<char> LogLevelChars =>
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

    [MethodImpl(MethodImplOptions.AggressiveInlining |
        MethodImplOptions.AggressiveOptimization)]
    public static ReadOnlySpan<char> GetShortName(LogLevel logLevel)
    {
        // Bounds checking with bitwise operation for performance
        if ((byte)logLevel >= MaxLogLevels)
        {
            // Fall back to the string representation for unknown levels
            return MemoryExtensions.AsSpan(logLevel.ToString().ToUpperInvariant());
        }

        // Get the pre-computed span for this log level
        return LogLevelChars.Slice((byte)logLevel * LogLevelPaddedLength, LogLevelLength);
    }

    #endregion APIs
}
