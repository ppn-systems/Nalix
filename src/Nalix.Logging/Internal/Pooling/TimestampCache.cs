// Copyright (c) 2025 PPN Corporation. All rights reserved.

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

    // Thread-local cache to avoid synchronization overhead
    [System.ThreadStatic]
    private static System.Int64 t_lastTicks;

    [System.ThreadStatic]
    private static System.String? t_cachedFormat;

    [System.ThreadStatic]
    private static System.String? t_cachedTimestamp;

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
    public static System.String GetFormattedTimestamp(System.DateTime timestamp, System.String format)
    {
        // Truncate to millisecond precision for caching
        System.Int64 currentTicks = timestamp.Ticks / System.TimeSpan.TicksPerMillisecond;

        // Fast path: same millisecond and format
        if (currentTicks == t_lastTicks && format == t_cachedFormat && t_cachedTimestamp != null)
        {
            return t_cachedTimestamp;
        }

        // Format the timestamp
        System.String formatted = timestamp.ToString(format, System.Globalization.CultureInfo.InvariantCulture);

        // Update cache
        t_cachedFormat = format;
        t_lastTicks = currentTicks;
        t_cachedTimestamp = formatted;

        return formatted;
    }

    #endregion Public Methods
}
