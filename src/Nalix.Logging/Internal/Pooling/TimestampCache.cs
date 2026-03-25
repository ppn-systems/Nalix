// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Logging.Internal.Pooling;

/// <summary>
/// High-performance timestamp cache that reuses formatted timestamps within the same millisecond.
/// Significantly reduces string allocation overhead for high-frequency logging scenarios.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
internal static class TimestampCache
{
    #region Fields

    /// <summary>
    /// Thread-local cache to avoid synchronization overhead
    /// </summary>
    [System.ThreadStatic]
    private static long t_lastTicks;

    [System.ThreadStatic]
    private static string? t_cachedFormat;

    [System.ThreadStatic]
    private static string? t_cachedTimestamp;

    #endregion Fields

    #region Public Methods

    /// <summary>
    /// Gets a formatted timestamp string, reusing cached value if within the same millisecond.
    /// </summary>
    /// <param name="timestamp">The DateTime to format.</param>
    /// <param name="format">The format string to use.</param>
    /// <returns>A formatted timestamp string.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static string GetFormattedTimestamp(System.DateTime timestamp, string format)
    {
        // Truncate to millisecond precision for caching
        long currentTicks = timestamp.Ticks / System.TimeSpan.TicksPerMillisecond;

        // Fast path: same millisecond and format
        if (currentTicks == t_lastTicks && format == t_cachedFormat && t_cachedTimestamp != null)
        {
            return t_cachedTimestamp;
        }

        // Format the timestamp
        string formatted = timestamp.ToString(format, System.Globalization.CultureInfo.InvariantCulture);

        // Update cache
        t_cachedFormat = format;
        t_lastTicks = currentTicks;
        t_cachedTimestamp = formatted;

        return formatted;
    }

    #endregion Public Methods
}
