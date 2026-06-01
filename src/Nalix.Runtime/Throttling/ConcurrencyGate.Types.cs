// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Extensions.Logging;
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
        /// <param name="logger">The logger used for entry diagnostics.</param>
        public Entry(int max, bool queue, int queueMax, ILogger? logger = null)
        {
            if (max <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(max), "Capacity must be positive");
            }

            this.Queue = queue;
            this.Capacity = max;
            this.QueueMax = queueMax < 0 ? int.MaxValue : queueMax;
            this.Sem = new SemaphoreSlim(this.Capacity, this.Capacity);
            this.Logger = logger;

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

        internal ILogger? Logger { get; }

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
                if (this.Logger != null && this.Logger.IsEnabled(LogLevel.Error))
                {
                    this.Logger.LogError("[RT.ConcurrencyGate:Entry] activeUsers overflow detected");
                }
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
                if (this.Logger != null && this.Logger.IsEnabled(LogLevel.Error))
                {
                    this.Logger.LogError("[RT.ConcurrencyGate:Entry] activeUsers underflow detected");
                }
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
                if (this.Logger != null && this.Logger.IsEnabled(LogLevel.Error))
                {
                    this.Logger.LogError("[RT.ConcurrencyGate:Entry] queueCount underflow detected");
                }
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
                    if (this.Logger != null && this.Logger.IsEnabled(LogLevel.Warning))
                    {
                        this.Logger.LogWarning("[RT.ConcurrencyGate:Entry] disposing with {ActiveUsers} active users", remainingUsers);
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
                    if (this.Logger != null && this.Logger.IsEnabled(LogLevel.Error))
                    {
                        this.Logger.LogError(ex, "[RT.ConcurrencyGate:Entry] disposal-error");
                    }
                }
            }
        }
    }

    #endregion Entry Class

    #region Lease Struct

    /// <summary>
    /// Represents a lease on a concurrency slot.
    /// Disposing this struct releases the slot back to the semaphore.
    /// </summary>
    /// <param name="sem"></param>
    /// <param name="entry"></param>
    public readonly struct Lease(SemaphoreSlim sem, Entry entry) : IDisposable
    {
        private readonly Entry _entry = entry ?? throw new ArgumentNullException(nameof(entry));
        private readonly SemaphoreSlim _sem = sem ?? throw new ArgumentNullException(nameof(sem));

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
                if (_entry.Logger != null && _entry.Logger.IsEnabled(LogLevel.Error))
                {
                    _entry.Logger.LogError(ex, "[RT.ConcurrencyGate:Lease] release-error");
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

