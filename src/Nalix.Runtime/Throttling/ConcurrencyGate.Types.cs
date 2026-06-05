// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Nalix.Abstractions.Diagnostics;
using Nalix.Abstractions.Exceptions;

namespace Nalix.Runtime.Throttling;

public sealed partial class ConcurrencyGate
{
    #region Entry Class

    /// <summary>
    /// Represents a per-opcode concurrency limiter with reference counting for safe disposal.
    /// Thread-safe implementation with proper synchronization.
    /// </summary>
    public sealed class Entry : IDisposable
    {
        private int _queueCount;
        /// <summary>
        /// Reference count
        /// </summary>
        private int _activeUsers;
        private long _lastUsedUtcTicks;
        private int _disposed;

        private readonly Lock _disposalLock = new();

        /// <summary>
        /// Gets a value indicating whether FIFO queuing is enabled for this entry.
        /// </summary>
        public bool Queue { get; }

        /// <summary>
        /// Gets the maximum number of concurrent operations allowed for this entry.
        /// </summary>
        public int Capacity { get; }

        /// <summary>
        /// Gets the maximum number of operations that can be queued for this entry.
        /// </summary>
        public int QueueMax { get; }

        /// <summary>
        /// Gets the semaphore used to enforce the concurrency limit for this entry.
        /// </summary>
        public SemaphoreSlim Sem { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Entry"/> class.
        /// </summary>
        /// <param name="max"></param>
        /// <param name="queue"></param>
        /// <param name="queueMax"></param>
        public Entry(int max, bool queue, int queueMax)
        {
            if (max <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(max), "Capacity must be positive");
            }

            this.Queue = queue;
            this.Capacity = max;
            this.QueueMax = queueMax < 0 ? int.MaxValue : queueMax;
            this.Sem = new SemaphoreSlim(this.Capacity, this.Capacity);

            _activeUsers = 0;
            _queueCount = 0;
            _disposed = 0;

            this.Touch();
        }

        /// <summary>
        /// Updates last used timestamp.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Touch()
        {
            long nowTicks = DateTime.UtcNow.Ticks;
            _ = Interlocked.Exchange(ref _lastUsedUtcTicks, nowTicks);
        }

        /// <summary>
        /// Gets the last used timestamp.
        /// </summary>
        public DateTimeOffset LastUsedUtc
        {
            get
            {
                long ticks = Interlocked.Read(ref _lastUsedUtcTicks);
                return new DateTimeOffset(ticks, TimeSpan.Zero);
            }
        }

        /// <summary>
        /// Gets current queue count.
        /// </summary>
        public int QueueCount => Volatile.Read(ref _queueCount);

        /// <summary>
        /// Entry is idle when no slots are in use and queue is empty.
        /// </summary>
        public bool IsIdle
        {
            get
            {
                if (Volatile.Read(ref _disposed) != 0)
                {
                    return false;
                }

                int activeUsers = Volatile.Read(ref _activeUsers);
                int queueCount = Volatile.Read(ref _queueCount);

                int available = this.Sem.CurrentCount;

                return activeUsers == 0 && available == this.Capacity && queueCount == 0;
            }
        }

        /// <summary>
        /// Attempts to acquire usage reference. Returns false if disposed.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAcquire()
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                return false;
            }

            int newCount = Interlocked.Increment(ref _activeUsers);

            // Double-check after increment
            if (Volatile.Read(ref _disposed) != 0)
            {
                _ = Interlocked.Decrement(ref _activeUsers);
                return false;
            }

            if (newCount <= 0) // Overflow detection
            {
                _ = Interlocked.Decrement(ref _activeUsers);
                LOG_OVERFLOW();
                return false;
            }

            return true;
        }

        /// <summary>
        /// Releases usage reference.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Release()
        {
            int remaining = Interlocked.Decrement(ref _activeUsers);

            if (remaining < 0)
            {
                LOG_UNDERFLOW();
                _ = Interlocked.Exchange(ref _activeUsers, 0);
            }
        }

        /// <summary>
        /// Attempts to increment queue count if under limit.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryIncrementQueue()
        {
            if (this.QueueMax == int.MaxValue)
            {
                _ = Interlocked.Increment(ref _queueCount);
                return true;
            }

            int next = Interlocked.Increment(ref _queueCount);
            if (next <= this.QueueMax)
            {
                return true;
            }

            int remaining = Interlocked.Decrement(ref _queueCount);
            if (remaining < 0)
            {
                _ = Interlocked.Exchange(ref _queueCount, 0);
            }

            return false;
        }

        /// <summary>
        /// Decrements queue count.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DecrementQueue()
        {
            int remaining = Interlocked.Decrement(ref _queueCount);

            if (remaining < 0)
            {
                LOG_QUEUE_UNDERFLOW();
                _ = Interlocked.Exchange(ref _queueCount, 0);
            }
        }

        /// <summary>
        /// Safely disposes the semaphore after waiting for active users.
        /// </summary>
        public void Dispose()
        {
            lock (_disposalLock)
            {
                // Atomic check-and-set: 0 -> 1
                if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
                {
                    return; // Already disposed
                }

                // Wait for active users with exponential backoff
                int waitedMs = 0;
                int backoffMs = 1;
                const int maxWaitMs = 500;
                const int maxBackoffMs = 50;

                while (Volatile.Read(ref _activeUsers) > 0 && waitedMs < maxWaitMs)
                {
                    Thread.Sleep(backoffMs);
                    waitedMs += backoffMs;
                    backoffMs = Math.Min(backoffMs * 2, maxBackoffMs);
                }

                int remainingUsers = Volatile.Read(ref _activeUsers);
                if (remainingUsers > 0)
                {
                    if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Warning))
                    {
                        DiagnosticsEvents.Source.Write(
                            DiagnosticsEvents.Internal.Warning,
                            new DiagnosticLog(
                                "RT.ConcurrencyGate:Entry",
                                $"disposing with active_users={remainingUsers}"));
                    }
                }

                // Dispose semaphore
                try
                {
                    this.Sem.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // Already disposed - acceptable
                }
                catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                {
                    if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Error))
                    {
                        DiagnosticsEvents.Source.Write(
                            DiagnosticsEvents.Internal.Error,
                            new DiagnosticLog(
                                "RT.ConcurrencyGate:Entry",
                                "disposal-error",
                                ex));
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void LOG_OVERFLOW()
        {
            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Error))
            {
                DiagnosticsEvents.Source.Write(
                    DiagnosticsEvents.Internal.Error,
                    new DiagnosticLog(
                        "RT.ConcurrencyGate:Entry",
                        "activeUsers overflow detected"));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void LOG_UNDERFLOW()
        {
            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Error))
            {
                DiagnosticsEvents.Source.Write(
                    DiagnosticsEvents.Internal.Error,
                    new DiagnosticLog(
                        "RT.ConcurrencyGate:Entry",
                        "activeUsers underflow detected"));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void LOG_QUEUE_UNDERFLOW()
        {
            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Error))
            {
                DiagnosticsEvents.Source.Write(
                    DiagnosticsEvents.Internal.Error,
                    new DiagnosticLog(
                        "RT.ConcurrencyGate:Entry",
                        "queueCount underflow detected"));
            }
        }
    }

    #endregion Entry Class

    #region Lease Struct

    /// <summary>
    /// Represents a lease on a concurrency slot.
    /// Disposing this struct releases the slot back to the semaphore.
    /// </summary>
    public readonly struct Lease : IDisposable
    {
        private readonly Entry _entry;
        private readonly SemaphoreSlim _sem;

        /// <summary>
        /// Initializes a new instance of the <see cref="Lease"/> struct.
        /// </summary>
        /// <param name="sem">The semaphore associated with the lease.</param>
        /// <param name="entry">The concurrency gate entry.</param>
        public Lease(SemaphoreSlim sem, Entry entry)
        {
            ArgumentNullException.ThrowIfNull(entry);
            ArgumentNullException.ThrowIfNull(sem);
            _entry = entry;
            _sem = sem;
        }

        /// <summary>
        /// Releases the concurrency slot.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            if (_sem is null || _entry is null)
            {
                return;
            }

            this.DISPOSE_INTERNAL();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void DISPOSE_INTERNAL()
        {
            try
            {
                _ = _sem.Release();
            }
            catch (ObjectDisposedException)
            {
                // Semaphore was disposed during cleanup - acceptable
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Error))
                {
                    DiagnosticsEvents.Source.Write(
                        DiagnosticsEvents.Internal.Error,
                        new DiagnosticLog(
                            "RT.ConcurrencyGate:Lease",
                            "release-error",
                        ex));
                }
            }
            finally
            {
                _entry.Release();
            }
        }
    }

    #endregion Lease Struct
}

