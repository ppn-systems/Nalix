// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Network.Internal.Time;

/// <summary>
/// Contains metrics about the TimingWheel execution.
/// </summary>
internal readonly struct TimingWheelMetrics
{
    public readonly int RegisteredCount;
    public readonly int PeakRegistered;
    public readonly long TotalRegistrations;
    public readonly long TotalUnregistrations;
    public readonly long TotalTimeouts;
    public readonly long TotalStaleSkips;
    public readonly long MaxTickDrift;

    public TimingWheelMetrics(int registeredCount, int peakRegistered, long totalRegistrations, long totalUnregistrations, long totalTimeouts, long totalStaleSkips, long maxTickDrift)
    {
        RegisteredCount = registeredCount;
        PeakRegistered = peakRegistered;
        TotalRegistrations = totalRegistrations;
        TotalUnregistrations = totalUnregistrations;
        TotalTimeouts = totalTimeouts;
        TotalStaleSkips = totalStaleSkips;
        MaxTickDrift = maxTickDrift;
    }
}
