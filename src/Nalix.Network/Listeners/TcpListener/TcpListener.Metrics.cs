// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Nalix.Network.Listeners.Tcp;

public abstract partial class TcpListenerBase
{
    #region Nested Metrics Class

    /// <summary>
    /// Metrics for tracking TCP listener connection lifecycle and errors.
    /// Lock-free, thread-safe, zero-allocation design using atomic operations.
    /// </summary>
    public sealed class LMetrics
    {
        #region Fields

        private long _totalErrors;
        private long _totalAccepted;
        private long _totalRejected;

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
        /// Gets the total number of rejected connections.
        /// </summary>
        public long TotalRejected => Volatile.Read(ref _totalRejected);

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
        /// Records an acceptance error.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RECORD_ERROR() => Interlocked.Increment(ref _totalErrors);

        #endregion Internal Methods
    }

    #endregion Nested Metrics Class

    /// <inheritdoc/>
    public LMetrics Metrics
    {
        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
    } = new();
}
