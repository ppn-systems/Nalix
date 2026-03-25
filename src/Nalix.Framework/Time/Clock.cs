// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Nalix.Framework.Time;

/// <summary>
/// Handles precise time for the system with high accuracy, supporting various time-related operations
/// required for real-time communication and distributed systems.
/// </summary>
[StackTraceHidden]
[DebuggerStepThrough]
public static partial class Clock
{
    #region Constants and Fields

    // BaseValue36 values for high-precision time calculation
    private static readonly DateTime UtcBase;
    private static readonly long UtcBaseTicks;
    private static readonly double DriftSmoothing;
    private static readonly Stopwatch UtcStopwatch;

    // Time synchronization variables
    private static long _timeOffset;
    private static double _driftCorrection;
    private static long _lastSyncMonoTicks;
    private static DateTime _lastExternalTime;
    private static readonly double _swToDateTimeTicks;

    #endregion Constants and Fields

    #region Properties

    /// <summary>
    /// Gets the frequency of the high-resolution timer in ticks per second.
    /// </summary>
    public static long TicksPerSecond => Stopwatch.Frequency;

    /// <summary>
    /// Gets a value indicating whether the clock has been synchronized with an external time source.
    /// </summary>
    public static bool IsSynchronized { get; private set; }

    /// <summary>
    /// Gets the time when the last synchronization occurred.
    /// </summary>
    public static DateTime LastSyncTime { get; private set; }

    #endregion Properties

    #region Constructors

    static Clock()
    {
        _timeOffset = 0; // In ticks, adjusted from external time sources
        _driftCorrection = 1.0; // Multiplier to correct for system clock drift
        _swToDateTimeTicks = (double)TimeSpan.TicksPerSecond / Stopwatch.Frequency;

        // Static class, no instantiation allowed
        DriftSmoothing = 0.1;
        IsSynchronized = false;
        UtcBase = DateTime.UtcNow;

        UtcBaseTicks = UtcBase.Ticks;
        LastSyncTime = DateTime.MinValue;
        UtcStopwatch = Stopwatch.StartNew();
    }

    #endregion Constructors

    #region Time Synchronization Methods

    /// <summary>
    /// Synchronizes the clock with an external time source.
    /// </summary>
    /// <param name="externalTime">The accurate external UTC time.</param>
    /// <param name="maxAllowedDriftMs">Maximum allowed drift in milliseconds before adjustment is applied.</param>
    /// <returns>The adjustment made in milliseconds.</returns>
    [MethodImpl(
        MethodImplOptions.NoInlining)]
    [return: NotNull]
    public static double SynchronizeTime(
        [NotNull] DateTime externalTime,
        [NotNull] double maxAllowedDriftMs = 1000.0)
    {
        if (externalTime.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("External time must be UTC", nameof(externalTime));
        }

        double diffMs = (externalTime - NowUtc()).TotalMilliseconds;
        if (Math.Abs(diffMs) <= maxAllowedDriftMs)
        {
            return 0;
        }

        long nowMono = Stopwatch.GetTimestamp();

        DateTime prevExt = _lastExternalTime;

        IsSynchronized = true;
        LastSyncTime = externalTime;

        Volatile.Write(ref _timeOffset, (long)(diffMs * TimeSpan.TicksPerMillisecond));

        if (prevExt != DateTime.MinValue)
        {
            double extElapsed = (externalTime - prevExt).TotalSeconds;
            long deltaMono = nowMono - _lastSyncMonoTicks;

            double monoElapsed = deltaMono / (double)Stopwatch.Frequency;
            if (monoElapsed > 60.0)
            {
                double drift = extElapsed / monoElapsed;
                double dc = _driftCorrection;
                dc += (drift - dc) * DriftSmoothing;   // optimized smoothing
                Volatile.Write(ref _driftCorrection, dc);
            }
        }

        _lastExternalTime = externalTime;
        _lastSyncMonoTicks = nowMono;

        return diffMs;
    }

    /// <summary>
    /// Applies time synchronization using a Unix timestamp and optional RTT.
    /// </summary>
    /// <param name="serverUnixMs">The server Unix timestamp in milliseconds. Must be non-negative and within a reasonable range.</param>
    /// <param name="rttMs">Round-trip time in milliseconds. Must be non-negative.</param>
    /// <param name="maxAllowedDriftMs">Maximum allowed drift in milliseconds before adjustment is applied. Must be positive.</param>
    /// <param name="maxHardAdjustMs">Maximum hard adjustment in milliseconds. Must be positive.</param>
    /// <returns>The adjustment made in milliseconds, or 0 if inputs are invalid or adjustment exceeds limits.</returns>
    /// <exception cref="ArgumentException">Thrown when input parameters are invalid.</exception>
    [MethodImpl(
        MethodImplOptions.AggressiveInlining)]
    [return: NotNull]
    public static double SynchronizeUnixMilliseconds(
        [NotNull] long serverUnixMs,
        [NotNull] double rttMs = 0,
        [NotNull] double maxAllowedDriftMs = 1_000.0,
        [NotNull] double maxHardAdjustMs = 10_000.0)
    {
        // Validate input parameters
        if (serverUnixMs < 0)
        {
            throw new ArgumentException("Server Unix timestamp cannot be negative", nameof(serverUnixMs));
        }

        if (rttMs < 0)
        {
            throw new ArgumentException("RTT cannot be negative", nameof(rttMs));
        }

        if (maxAllowedDriftMs <= 0)
        {
            throw new ArgumentException("Max allowed drift must be positive", nameof(maxAllowedDriftMs));
        }

        if (maxHardAdjustMs <= 0)
        {
            throw new ArgumentException("Max hard adjust must be positive", nameof(maxHardAdjustMs));
        }

        // Sanity check: Unix timestamp should be reasonable (after year 2000 and before year 2100)
        const long MinReasonableUnixMs = 946684800000L; // Jan 1, 2000
        const long MaxReasonableUnixMs = 4102444800000L; // Jan 1, 2100
        if (serverUnixMs is < MinReasonableUnixMs or > MaxReasonableUnixMs)
        {
            throw new ArgumentException("Server Unix timestamp is outside reasonable range (2000-2100)", nameof(serverUnixMs));
        }

        // Compensate half RTT (one-way latency)
        long corrected = serverUnixMs + (long)(rttMs * 0.5);
        DateTime externalTime = DateTime.UnixEpoch.AddMilliseconds(corrected);

        double adjustMs = (externalTime - NowUtc()).TotalMilliseconds;
        return Math.Abs(adjustMs) > maxHardAdjustMs ? 0 : SynchronizeTime(externalTime, maxAllowedDriftMs);
    }

    /// <summary>
    /// Resets time synchronization to use the local system time.
    /// </summary>
    [MethodImpl(
        MethodImplOptions.NoInlining)]
    [return: NotNull]
    public static void ResetSynchronization()
    {
        _ = Interlocked.Exchange(ref _timeOffset, 0);
        _ = Interlocked.Exchange(ref _driftCorrection, 1.0);

        IsSynchronized = false;
        LastSyncTime = DateTime.MinValue;
    }

    /// <summary>
    /// Gets the estimated clock drift rate.
    /// A value greater than 1.0 means the local clock is running slower than the reference clock.
    /// A value less than 1.0 means the local clock is running faster than the reference clock.
    /// </summary>
    [MethodImpl(
        MethodImplOptions.AggressiveOptimization)]
    [return: NotNull]
    public static double DriftRate() => Volatile.Read(ref _driftCorrection);

    /// <summary>
    /// Gets the current error estimate between the synchronized time and system time in milliseconds.
    /// </summary>
    [MethodImpl(
        MethodImplOptions.AggressiveOptimization)]
    [return: NotNull]
    public static double CurrentErrorEstimateMs()
    {
        if (!IsSynchronized)
        {
            return 0;
        }

        DateTime driftedTime = NowUtc();
        DateTime systemTime = DateTime.UtcNow;
        return (driftedTime - systemTime).TotalMilliseconds;
    }

    #endregion Time Synchronization Methods
}
