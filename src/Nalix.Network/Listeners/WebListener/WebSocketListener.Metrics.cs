// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Nalix.Network.Listeners.Web;

public abstract partial class WebSocketListenerBase
{
    #region Nested Metrics Class

    /// <summary>
    /// Metrics for tracking WebSocket listener connection lifecycle and errors.
    /// Lock-free, thread-safe, zero-allocation design using atomic operations.
    /// </summary>
    public sealed class WMetrics
    {
        #region Fields

        private long _totalErrors;
        private long _totalAccepted;
        private long _totalRejected;
        private long _totalQueueFullRejections;
        private long _totalLimiterRejections;

        #endregion Fields

        #region Properties

        /// <summary>
        /// Gets the total number of acceptance errors.
        /// </summary>
        public long TotalErrors => Volatile.Read(ref _totalErrors);

        /// <summary>
        /// Gets the total number of accepted connections.
        /// </summary>
        public long TotalAccepted => Volatile.Read(ref _totalAccepted);

        /// <summary>
        /// Gets the total number of rejected connections (includes queue-full and limiter rejections).
        /// </summary>
        public long TotalRejected => Volatile.Read(ref _totalRejected) + this.TotalQueueFullRejections + this.TotalLimiterRejections;

        /// <summary>
        /// Gets the total number of queue full rejections.
        /// </summary>
        public long TotalQueueFullRejections => Volatile.Read(ref _totalQueueFullRejections);

        /// <summary>
        /// Gets the total number of connection limiter/guard rejections.
        /// </summary>
        public long TotalLimiterRejections => Volatile.Read(ref _totalLimiterRejections);

        #endregion Properties

        #region Internal Methods

        /// <summary>
        /// Records a successfully accepted connection.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RECORD_ACCEPTED() => Interlocked.Increment(ref _totalAccepted);

        /// <summary>
        /// Records a rejected connection attempt.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RECORD_REJECTED() => Interlocked.Increment(ref _totalRejected);

        /// <summary>
        /// Records a queue full rejection.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RECORD_QUEUE_FULL_REJECTION() => Interlocked.Increment(ref _totalQueueFullRejections);

        /// <summary>
        /// Records a limiter/guard connection rejection.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RECORD_LIMITER_REJECTION() => Interlocked.Increment(ref _totalLimiterRejections);

        /// <summary>
        /// Records an acceptance error.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RECORD_ERROR() => Interlocked.Increment(ref _totalErrors);

        #endregion Internal Methods
    }

    #endregion Nested Metrics Class

    /// <inheritdoc/>
    public new WMetrics Metrics
    {
        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
    } = new();
}
