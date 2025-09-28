// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

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

        private System.Int64 _totalErrors;
        private System.Int64 _totalAccepted;
        private System.Int64 _totalRejected;

        #endregion Fields

        #region Properties

        /// <summary>
        /// Gets the total number of acceptance errors.
        /// </summary>
        public System.Int64 TotalErrors => System.Threading.Volatile.Read(ref _totalErrors);

        /// <summary>
        /// Gets the total number of accepted connections.
        /// </summary>
        public System.Int64 TotalAccepted => System.Threading.Volatile.Read(ref _totalAccepted);

        /// <summary>
        /// Gets the total number of rejected connections.
        /// </summary>
        public System.Int64 TotalRejected => System.Threading.Volatile.Read(ref _totalRejected);

        #endregion Properties

        #region Internal Methods

        /// <summary>
        /// Records a successfully accepted connection.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        internal void RECORD_ACCEPTED() => System.Threading.Interlocked.Increment(ref _totalAccepted);

        /// <summary>
        /// Records a rejected connection attempt.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        internal void RECORD_REJECTED() => System.Threading.Interlocked.Increment(ref _totalRejected);

        /// <summary>
        /// Records an acceptance error.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        internal void RECORD_ERROR() => System.Threading.Interlocked.Increment(ref _totalErrors);

        #endregion Internal Methods
    }

    #endregion Nested Metrics Class

    private readonly LMetrics _metrics = new();

    /// <inheritdoc/>
    public LMetrics Metrics
    {
        [System.Diagnostics.DebuggerStepThrough]
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        get => _metrics;
    }

}
