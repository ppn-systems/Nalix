// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Common.Diagnostics;

/// <summary>
/// Represents the state for logging that supports throttling.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="LogThrottleState"/> struct.
/// </remarks>
/// <param name="suppressWindow">The time window during which logs are suppressed.</param>
public struct LogThrottleState(System.TimeSpan suppressWindow)
{
    /// <summary>
    /// The timestamp (ticks) of the last recorded log.
    /// </summary>
    public System.Int64 LastLogTimeTicks = 0;

    /// <summary>
    /// The count of log messages suppressed since the last log.
    /// </summary>
    public System.Int64 SuppressedCount = 0;

    /// <summary>
    /// The time window in ticks where repeated log messages are suppressed.
    /// </summary>
    public System.Int64 SuppressWindowTicks = suppressWindow.Ticks;
}
