// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Threading;

namespace Nalix.Network.RateLimiting;

public sealed partial class ConnectionGuard
{
    internal readonly struct ConnectionAllowResult
    {
        public bool Allowed { get; init; }
        public int CurrentConnections { get; init; }
    }

    /// <summary>
    /// Immutable snapshot of connection tracking data for an endpoint.
    /// Used as the value type for CAS-style updates within a locked <see cref="ConnectionLimitEntry"/>.
    /// </summary>
    [DebuggerDisplay("Current={CurrentConnections}, Today={TotalConnectionsToday}, Last={LastConnectionTime}")]
    internal readonly record struct ConnectionLimitInfo
    {
        /// <summary>Current number of active connections.</summary>
        public int CurrentConnections { get; init; }

        /// <summary>Timestamp of most recent connection activity.</summary>
        public DateTime LastConnectionTime { get; init; }

        /// <summary>Total connections established today (resets daily).</summary>
        public int TotalConnectionsToday { get; init; }

        public ConnectionLimitInfo(
            int currentConnections,
            DateTime lastConnectionTime,
            int totalConnectionsToday)
        {
            this.CurrentConnections = currentConnections;
            this.LastConnectionTime = lastConnectionTime;
            this.TotalConnectionsToday = totalConnectionsToday;
        }
    }

    /// <summary>
    /// Mutable container for one endpoint's tracking state.
    /// <para>
    /// <see cref="Info"/> is a value-type snapshot; mutations must be done inside
    /// <c>lock(entry)</c> to avoid torn reads/writes under concurrent access.
    /// </para>
    /// <para>
    /// <see cref="RecentConnectionTimestamps"/> is a <see cref="System.Collections.Concurrent.ConcurrentQueue{T}"/>
    /// and can be trimmed lock-free; enqueues happen inside the lock alongside the Info update.
    /// </para>
    /// </summary>
    internal sealed class ConnectionLimitEntry
    {
        public bool IsRemoved;
        public long BannedUntilTicks;

        /// <summary>
        /// Track the progressive ban tier.
        /// </summary>
        public int BanCount;

        /// <summary>
        /// Last time this IP was banned. Used for ban count decay.
        /// </summary>
        public long LastBanTimeTicks;

        /// <summary>
        /// Last time any network activity was seen from this IP.
        /// </summary>
        public long LastSeenAtTicks;

        /// <summary>
        /// lần cuối log DDoS warn
        /// </summary>
        public long LastDDoSLogTicks;
        /// <summary>
        /// số lần bị suppress
        /// </summary>
        public long SuppressedDDoSCount;

        /// <summary>
        /// Reject log throttle (new)
        /// </summary>
        public long LastRejectLogTicks;
        public long SuppressedRejectCount;

        /// <summary>
        /// Closed log throttle (new)
        /// </summary>
        public long LastClosedLogTicks;
        public long SuppressedClosedCount;

        /// <summary>
        /// Mutable connection info. Access only inside <c>SpinLock</c>.
        /// </summary>
        public ConnectionLimitInfo Info;

        /// <summary>
        /// SpinLock for micro-operations. Faster than Monitor.
        /// </summary>
        public SpinLock SpinLock = new(false);

        /// <summary>
        /// Sliding-window timestamps for rate limiting.
        /// </summary>
        public readonly System.Collections.Generic.Queue<long> RecentConnectionTimestamps = new();
    }
}
