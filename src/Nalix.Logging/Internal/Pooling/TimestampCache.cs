// Copyright (c) 2025 PPN Corporation. All rights reserved.

using System.Runtime.CompilerServices;

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
    private static System.String? t_cachedTimestamp;

    [System.ThreadStatic]
    private static System.String? t_cachedFormat;

    #endregion Fields

    #region Public Methods

    /// <summary>
    /// Gets a formatted timestamp string, reusing cached value if within the same millisecond.
    /// </summary>
    /// <param name="timestamp">The DateTime to format.</param>
    /// <param name="format">The format string to use.</param>
    /// <returns>A formatted timestamp string.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        t_lastTicks = currentTicks;
        t_cachedTimestamp = formatted;
        t_cachedFormat = format;

        return formatted;
    }

    /// <summary>
    /// Tries to format a timestamp into a span, using cached value when possible.
    /// </summary>
    /// <param name="timestamp">The DateTime to format.</param>
    /// <param name="format">The format string to use.</param>
    /// <param name="destination">The destination span to write to.</param>
    /// <param name="charsWritten">The number of characters written.</param>
    /// <returns>True if formatting succeeded, false otherwise.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static System.Boolean TryFormatTimestamp(
        System.DateTime timestamp,
        System.ReadOnlySpan<System.Char> format,
        System.Span<System.Char> destination,
        out System.Int32 charsWritten)
    {
        // Try direct formatting first (most efficient)
        if (timestamp.TryFormat(destination, out charsWritten, format, System.Globalization.CultureInfo.InvariantCulture))
        {
            return true;
        }

        charsWritten = 0;
        return false;
    }

    #endregion Public Methods
}
