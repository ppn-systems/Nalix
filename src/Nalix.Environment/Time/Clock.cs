// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;

namespace Nalix.Environment.Time;

/// <summary>
/// Provides time synchronization and timestamp helpers.
/// The clock keeps a local estimate of UTC that can be nudged toward an
/// external reference without replacing the system clock.
/// </summary>
[StackTraceHidden]
[DebuggerStepThrough]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "<Pending>")]
public static partial class Clock
{
    #region Constants and Fields

    // Baseline values used to anchor the monotonic stopwatch to UTC time.
    private static readonly DateTime s_utcBase;
    private static readonly long s_utcBaseTicks;
    private static readonly Stopwatch s_utcStopwatch;
    private static readonly double s_swToDateTimeTicks;

    #endregion Constants and Fields

    #region Properties

    /// <summary>
    /// Gets the frequency of the high-resolution timer in ticks per second.
    /// </summary>
    public static long TicksPerSecond => Stopwatch.Frequency;

    #endregion Properties

    #region Constructors

    static Clock()
    {
        // The initial estimate is based on the current UTC time and then refined
        // by later synchronization calls.
        s_utcStopwatch = Stopwatch.StartNew();
        s_utcBase = DateTime.UtcNow;
        s_utcBaseTicks = s_utcBase.Ticks;
        s_swToDateTimeTicks = (double)TimeSpan.TicksPerSecond / Stopwatch.Frequency;
    }

    #endregion Constructors
}
