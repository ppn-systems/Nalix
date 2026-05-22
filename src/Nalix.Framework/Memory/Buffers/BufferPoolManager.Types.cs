// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Framework.Memory.Buffers;

public sealed partial class BufferPoolManager
{
    #region Nested Types

    /// <summary>
    /// Safety policy for shrinking operations.
    /// The shrink path is intentionally conservative so trimming never removes too
    /// much capacity at once and causes the next burst to allocate again.
    /// </summary>
    private sealed class ShrinkSafetyPolicy
    {
        /// <summary>
        /// Minimum percentage of total buffers to retain.
        /// This floor protects the pool from shrinking itself into constant churn.
        /// </summary>
        public double MinimumRetentionPercent { get; set; } = 0.25;

        /// <summary>
        /// Maximum buffers to shrink in a single operation.
        /// This caps the amount of memory the trimmer can remove in one pass.
        /// </summary>
        public int MaxSingleShrinkStep { get; set; } = 20;

        /// <summary>
        /// Maximum percentage of total buffers to shrink per trim cycle.
        /// This prevents a single trim job from collapsing the pool too aggressively.
        /// </summary>
        public double MaxShrinkPercentPerCycle { get; set; } = 0.20;

        /// <summary>
        /// Minimum absolute buffers per pool.
        /// The pool always keeps at least one buffer alive so it can recover quickly.
        /// </summary>
        public int AbsoluteMinimum { get; set; } = 1;
    }

    /// <summary>
    /// Metrics for tracking shrink/expand operations on a pool.
    /// These counters are used for diagnostics and for validating trim safety
    /// decisions over time.
    /// </summary>
    private struct BufferPoolMetrics
    {
        /// <summary>
        /// Total bytes returned to ArrayPool via shrinking.
        /// This is the amount of memory the pool actually gave back.
        /// </summary>
        public long TotalBytesReturned;

        /// <summary>
        /// Number of successful shrink operations.
        /// Useful for seeing whether trimming is actively doing work or mostly idle.
        /// </summary>
        public int ShrinkAttempted;

        /// <summary>
        /// Number of shrinks skipped due to safety checks.
        /// High values here usually mean the pool is already at or near its floor.
        /// </summary>
        public int ShrinkSkipped;

        /// <summary>
        /// Number of successful expand operations.
        /// This tells us how often the pool had to grow to satisfy demand.
        /// </summary>
        public int ExpandAttempted;

        /// <summary>
        /// Last timestamp when pool state changed.
        /// Helps correlate trimming decisions with recent allocation pressure.
        /// </summary>
        public long LastChangeTime;
    }

    #endregion Nested Types
}

