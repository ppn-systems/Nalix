// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

namespace Nalix.Framework.Memory.Objects;

public sealed partial class ObjectPoolManager
{
    #region Nested Types

    /// <summary>
    /// Detailed metrics for tracking pool performance and health.
    /// </summary>
    private sealed class PoolMetrics
    {
        public long TotalGets;
        public long TotalReturns;

        /// <summary>
        /// Failed to get from pool, created new
        /// </summary>
        public long CacheMisses;

        /// <summary>
        /// Got from pool successfully
        /// </summary>
        public long CacheHits;

        public long TotalCreated;
        public long TotalDisposed;
        public DateTime LastAccessUtc;
        public string? LastAccessType;
        public int ConsecutiveFailures;

        /// <summary>
        /// Number of objects currently checked out (Get without Return)
        /// </summary>
        public long Outstanding;

        /// <summary>
        /// Maximum concurrent outstanding objects recorded.
        /// </summary>
        public long PeakOutstanding;

        // Diagnostic Metrics (Only populated when diagnostics enabled)
        public long TotalLifetimeTicks;
        public long MaxLifetimeTicks;
        public long[]? LifetimeReservoir;
        public int ReservoirIndex;

        public long LastHealthGets;
        public long LastHealthHits;
        public long LastHealthMisses;
        public long LastHealthPeakOutstanding;
        public long LastPoolFailureLogUtcTicks;
        public int LastPoolFailureSeverity;
    }

    #endregion Nested Types

}

