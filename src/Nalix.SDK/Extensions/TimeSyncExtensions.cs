// Copyright (c) 2025 PPN.

using Nalix.Framework.Time;             // Clock
using System.Threading.Tasks;

namespace Nalix.SDK.Extensions;

/// <summary>
/// Time utilities for ReliableClient powered by the shared Clock.
/// </summary>
public static class TimeSyncExtensions
{
    /// <summary>
    /// Apply server-provided time (Unix ms) plus measured RTT (ms) to synchronize the local Clock.
    /// The one-way delay is approximated as RTT/2 to reduce bias.
    /// </summary>
    /// <param name="serverUnixMs">Server UTC time in milliseconds since Unix epoch.</param>
    /// <param name="rttMs">Measured round-trip latency in milliseconds.</param>
    public static void ApplyServerTimeSync(System.Int64 serverUnixMs, System.Double rttMs)
    {
        if (rttMs < 0)
        {
            rttMs = 0;
        }

        var estimatedOneWayMs = rttMs * 0.5;
        var serverUtc = System.DateTime.UnixEpoch.AddMilliseconds(serverUnixMs + estimatedOneWayMs);
        _ = Clock.SynchronizeTime(serverUtc);
    }

    /// <summary>Returns precise UTC now from the shared Clock.</summary>
    public static System.DateTime UtcNowPrecise() => Clock.GetUtcNowPrecise();

    /// <summary>Returns monotonic timestamp ticks (Stopwatch.GetTimestamp()).</summary>
    public static System.Int64 MonoTicks() => Clock.MonoTicksNow();

    /// <summary>Measure an async operation duration (ms) using the shared Clock.</summary>
    public static async Task<System.Double> MeasureAsync(System.Func<Task> op)
    {
        System.ArgumentNullException.ThrowIfNull(op);

        Clock.StartMeasurement();
        await op().ConfigureAwait(false);
        return Clock.GetElapsedMilliseconds();
    }
}
