// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Framework.Time;

/// <summary>
/// Handles precise time for the system with high accuracy, supporting various time-related operations
/// required for real-time communication and distributed systems.
/// </summary>
[System.Diagnostics.StackTraceHidden]
[System.Diagnostics.DebuggerStepThrough]
public static partial class Clock
{
    #region Constants and Fields

    // BaseValue36 values for high-precision time calculation
    private static readonly System.DateTime UtcBase;
    private static readonly System.Int64 UtcBaseTicks;
    private static readonly System.Double DriftSmoothing;
    private static readonly System.Diagnostics.Stopwatch UtcStopwatch;

    // Time synchronization variables
    private static System.Int64 _timeOffset;
    private static System.Double _driftCorrection;
    private static System.Int64 _lastSyncMonoTicks;
    private static System.DateTime _lastExternalTime;
    private static readonly System.Double _swToDateTimeTicks;

    #endregion Constants and Fields

    #region Properties

    /// <summary>
    /// Gets the frequency of the high-resolution timer in ticks per second.
    /// </summary>
    public static System.Int64 TicksPerSecond => System.Diagnostics.Stopwatch.Frequency;

    /// <summary>
    /// Gets a value indicating whether the clock has been synchronized with an external time source.
    /// </summary>
    public static System.Boolean IsSynchronized { get; private set; }

    /// <summary>
    /// Gets the time when the last synchronization occurred.
    /// </summary>
    public static System.DateTime LastSyncTime { get; private set; }

    #endregion Properties

    #region Constructors

    static Clock()
    {
        _timeOffset = 0; // In ticks, adjusted from external time sources
        _driftCorrection = 1.0; // Multiplier to correct for system clock drift
        _swToDateTimeTicks = (System.Double)System.TimeSpan.TicksPerSecond / System.Diagnostics.Stopwatch.Frequency;

        // Static class, no instantiation allowed
        DriftSmoothing = 0.1;
        IsSynchronized = false;
        UtcBase = System.DateTime.UtcNow;

        UtcBaseTicks = UtcBase.Ticks;
        LastSyncTime = System.DateTime.MinValue;
        UtcStopwatch = System.Diagnostics.Stopwatch.StartNew();
    }

    #endregion Constructors

    #region Time Synchronization Methods

    /// <summary>
    /// Synchronizes the clock with an external time source.
    /// </summary>
    /// <param name="externalTime">The accurate external UTC time.</param>
    /// <param name="maxAllowedDriftMs">Maximum allowed drift in milliseconds before adjustment is applied.</param>
    /// <returns>The adjustment made in milliseconds.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Double SynchronizeTime(
        [System.Diagnostics.CodeAnalysis.NotNull] System.DateTime externalTime,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Double maxAllowedDriftMs = 1000.0)
    {
        if (externalTime.Kind != System.DateTimeKind.Utc)
        {
            throw new System.ArgumentException("External time must be UTC", nameof(externalTime));
        }

        System.Double diffMs = (externalTime - NowUtc()).TotalMilliseconds;
        if (System.Math.Abs(diffMs) <= maxAllowedDriftMs)
        {
            return 0;
        }

        System.Int64 nowMono = System.Diagnostics.Stopwatch.GetTimestamp();

        var prevExt = _lastExternalTime;

        IsSynchronized = true;
        LastSyncTime = externalTime;

        System.Threading.Volatile.Write(ref _timeOffset, (System.Int64)(diffMs * System.TimeSpan.TicksPerMillisecond));

        if (prevExt != System.DateTime.MinValue)
        {
            System.Double extElapsed = (externalTime - prevExt).TotalSeconds;
            System.Int64 deltaMono = nowMono - _lastSyncMonoTicks;

            System.Double monoElapsed = deltaMono / (System.Double)System.Diagnostics.Stopwatch.Frequency;
            if (monoElapsed > 60.0)
            {
                System.Double drift = extElapsed / monoElapsed;
                System.Double dc = _driftCorrection;
                dc += (drift - dc) * DriftSmoothing;   // optimized smoothing
                System.Threading.Volatile.Write(ref _driftCorrection, dc);
            }
        }

        _lastExternalTime = externalTime;
        _lastSyncMonoTicks = nowMono;

        return diffMs;
    }

    /// <summary>
    /// Applies time synchronization using a Unix timestamp and optional RTT.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Double SynchronizeUnixMilliseconds(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Int64 serverUnixMs,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Double rttMs = 0,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Double maxAllowedDriftMs = 1_000.0,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Double maxHardAdjustMs = 10_000.0)
    {
        // Compensate half RTT (one-way latency)
        System.Int64 corrected = serverUnixMs + (System.Int64)(rttMs * 0.5);
        System.DateTime externalTime = System.DateTime.UnixEpoch.AddMilliseconds(corrected);

        System.Double adjustMs = (externalTime - NowUtc()).TotalMilliseconds;
        return System.Math.Abs(adjustMs) > maxHardAdjustMs ? 0 : SynchronizeTime(externalTime, maxAllowedDriftMs);
    }

    /// <summary>
    /// Resets time synchronization to use the local system time.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static void ResetSynchronization()
    {
        _ = System.Threading.Interlocked.Exchange(ref _timeOffset, 0);
        _ = System.Threading.Interlocked.Exchange(ref _driftCorrection, 1.0);

        IsSynchronized = false;
        LastSyncTime = System.DateTime.MinValue;
    }

    /// <summary>
    /// Gets the estimated clock drift rate.
    /// A value greater than 1.0 means the local clock is running slower than the reference clock.
    /// A value less than 1.0 means the local clock is running faster than the reference clock.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Double DriftRate() => System.Threading.Volatile.Read(ref _driftCorrection);

    /// <summary>
    /// Gets the current error estimate between the synchronized time and system time in milliseconds.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    public static System.Double CurrentErrorEstimateMs()
    {
        if (!IsSynchronized)
        {
            return 0;
        }

        var driftedTime = NowUtc();
        var systemTime = System.DateTime.UtcNow;
        return (driftedTime - systemTime).TotalMilliseconds;
    }

    #endregion Time Synchronization Methods
}
