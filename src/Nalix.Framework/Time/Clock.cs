// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Nalix.Framework.Time;

/// <summary>
/// Provides time synchronization and timestamp helpers.
/// The clock keeps a local estimate of UTC that can be nudged toward an
/// external reference without replacing the system clock.
/// </summary>
[StackTraceHidden]
[DebuggerStepThrough]
public static partial class Clock
{
    #region Constants and Fields

    // Baseline values used to anchor the monotonic stopwatch to UTC time.
    private static readonly DateTime UtcBase;
    private static readonly long UtcBaseTicks;
    private static readonly double DriftSmoothing;
    private static readonly Stopwatch UtcStopwatch;

    // Synchronization state: offset, drift correction, and last sync markers.
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
        _timeOffset = 0; // UTC offset relative to the monotonic estimate, in ticks.
        _driftCorrection = 1.0; // Drift multiplier applied to elapsed stopwatch time.
        _swToDateTimeTicks = (double)TimeSpan.TicksPerSecond / Stopwatch.Frequency;

        // The initial estimate is based on the current UTC time and then refined
        // by later synchronization calls.
        DriftSmoothing = 0.1;
        IsSynchronized = false;
        UtcBase = DateTime.UtcNow;

        UtcBaseTicks = UtcBase.Ticks;
        LastSyncTime = DateTime.MinValue;
        UtcStopwatch = Stopwatch.StartNew();
    }

    #endregion Constructors

    #region Time Synchronization Methods

    /// <summary>Synchronizes the clock with an external UTC time source.</summary>
    /// <param name="externalTime">The external UTC time to synchronize against.</param>
    /// <param name="maxAllowedDriftMs">The maximum drift, in milliseconds, allowed before the correction is skipped.</param>
    /// <returns>The applied adjustment, in milliseconds.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="externalTime"/> is not UTC.</exception>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static double SynchronizeTime(
        DateTime externalTime,
        double maxAllowedDriftMs = 1000.0)
    {
        if (externalTime.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("External time must be UTC", nameof(externalTime));
        }

        // Compare the reference time against the current local estimate first.
        double diffMs = (externalTime - NowUtc()).TotalMilliseconds;
        if (Math.Abs(diffMs) <= maxAllowedDriftMs)
        {
            // If the difference is small enough, keep the current estimate rather
            // than forcing a correction that would just introduce noise.
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
            // Update the drift estimate only when enough monotonic time has passed
            // to make the ratio meaningful.
            if (monoElapsed > 60.0)
            {
                double drift = extElapsed / monoElapsed;
                double dc = _driftCorrection;
                // Smooth the correction so one noisy sync sample does not swing the
                // clock estimate too aggressively.
                dc += (drift - dc) * DriftSmoothing;
                Volatile.Write(ref _driftCorrection, dc);
            }
        }

        _lastExternalTime = externalTime;
        _lastSyncMonoTicks = nowMono;

        return diffMs;
    }

    /// <summary>Synchronizes the clock using a Unix timestamp and optional round-trip time.</summary>
    /// <param name="serverUnixMs">The server Unix timestamp, in milliseconds.</param>
    /// <param name="rttMs">The measured round-trip time, in milliseconds.</param>
    /// <param name="maxAllowedDriftMs">The maximum drift, in milliseconds, allowed before the correction is skipped.</param>
    /// <param name="maxHardAdjustMs">The maximum absolute correction, in milliseconds, allowed for a single adjustment.</param>
    /// <returns>The applied adjustment, in milliseconds, or 0 when the correction is rejected.</returns>
    /// <exception cref="ArgumentException">Thrown when an input value is invalid.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double SynchronizeUnixMilliseconds(
        long serverUnixMs,
        double rttMs = 0,
        double maxAllowedDriftMs = 1_000.0,
        double maxHardAdjustMs = 10_000.0)
    {
        // Validate input parameters up front so the correction logic only runs on
        // sane values.
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

        // Sanity check: Unix timestamp should be reasonable (after year 2000 and
        // before year 2100) so obviously bad input does not skew the local clock.
        const long MinReasonableUnixMs = 946684800000L; // Jan 1, 2000
        const long MaxReasonableUnixMs = 4102444800000L; // Jan 1, 2100
        if (serverUnixMs is < MinReasonableUnixMs or > MaxReasonableUnixMs)
        {
            throw new ArgumentException("Server Unix timestamp is outside reasonable range (2000-2100)", nameof(serverUnixMs));
        }

        // Compensate half the round-trip time as an estimate of one-way latency.
        long corrected = serverUnixMs + (long)(rttMs * 0.5);
        DateTime externalTime = DateTime.UnixEpoch.AddMilliseconds(corrected);

        double adjustMs = (externalTime - NowUtc()).TotalMilliseconds;
        return Math.Abs(adjustMs) > maxHardAdjustMs ? 0 : SynchronizeTime(externalTime, maxAllowedDriftMs);
    }

    /// <summary>Resets time synchronization to use the local system clock.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ResetSynchronization()
    {
        // Clear both the offset and the drift estimate so future reads fall back
        // to the local system clock until a new synchronization arrives.
        _ = Interlocked.Exchange(ref _timeOffset, 0);
        _ = Interlocked.Exchange(ref _driftCorrection, 1.0);

        IsSynchronized = false;
        LastSyncTime = DateTime.MinValue;
    }

    /// <summary>Gets the estimated clock drift rate.</summary>
    /// <returns>A value greater than 1.0 indicates the local clock is slower than the reference clock.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static double DriftRate() => Volatile.Read(ref _driftCorrection);

    /// <summary>Gets the current error estimate between synchronized time and system time, in milliseconds.</summary>
    /// <returns>The current error estimate, in milliseconds.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
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
