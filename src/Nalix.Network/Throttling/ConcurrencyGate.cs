// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Diagnostics;
using Nalix.Common.Exceptions;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Shared;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;

namespace Nalix.Network.Throttling;

/// <summary>
/// High-performance per-opcode concurrency limiter with optional FIFO queuing.
/// Thread-safe with reference counting for safe disposal.
/// Automatically cleans up idle entries to prevent memory leaks.
/// </summary>
[DebuggerNonUserCode]
[SkipLocalsInit]
public sealed class ConcurrencyGate : IReportable
{
    #region Constants

    private const double CircuitBreakerThreshold = 0.95;
    private const int CircuitBreakerMinSamples = 1000;
    private const int CircuitBreakerResetAfterSeconds = 60;

    private readonly TimeSpan MinIdleAge = TimeSpan.FromMinutes(10);
    private readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(1);

    #endregion Constants

    #region Fields

    private readonly System.Collections.Concurrent.ConcurrentDictionary<ushort, Entry> s_table = new();

    private static readonly ILogger s_logger = InstanceManager.Instance.GetExistingInstance<ILogger>()!;

    private long _totalAcquired;
    private long _totalRejected;
    private long _totalQueued;
    private long _totalCleanedEntries;
    private long _circuitBreakerTrips;
    private readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    /// <summary>
    /// FIX #1: Circuit breaker state management
    /// </summary>
    private int _circuitBreakerOpen; // 0 = closed, 1 = open
    private long _circuitBreakerResetTimeTicks;

    #endregion Fields

    #region Static Constructor

    /// <summary>
    /// Initializes the cleanup task.
    /// </summary>
    public ConcurrencyGate()
    {
        try
        {
            _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleRecurring(
                name: "concurrency.gate.cleanup",
                interval: CleanupInterval,
                work: _ =>
                {
                    CLEANUP_IDLE_ENTRIES();
                    return ValueTask.CompletedTask;
                },
                options: new RecurringOptions
                {
                    NonReentrant = true,
                    Tag = TaskNaming.Tags.Service,
                    Jitter = TimeSpan.FromSeconds(10),
                    ExecutionTimeout = TimeSpan.FromSeconds(5)
                });

            s_logger?.Debug($"[NW.{nameof(ConcurrencyGate)}] initialized with cleanup interval={CleanupInterval.TotalMinutes:F1}min");
        }
        catch (Exception ex)
        {
            s_logger?.Error($"[NW.{nameof(ConcurrencyGate)}] initialization-error", ex);
        }
    }

    #endregion Static Constructor

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

        /// <summary>
        /// FIX #2: Add lock for disposal coordination
        /// </summary>
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

            Queue = queue;
            Capacity = max;
            QueueMax = queueMax < 0 ? int.MaxValue : queueMax;
            Sem = new SemaphoreSlim(Capacity, Capacity);

            _activeUsers = 0;
            _queueCount = 0;
            _disposed = 0;

            Touch();
        }

        /// <summary>
        /// Updates last used timestamp.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Touch()
        {
            long nowTicks = DateTimeOffset.UtcNow.UtcDateTime.Ticks;
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

                // FIX #3: Use SpinLock for atomic read of semaphore state
                int available = Sem.CurrentCount;

                return activeUsers == 0 && available == Capacity && queueCount == 0;
            }
        }

        /// <summary>
        /// Attempts to acquire usage reference. Returns false if disposed.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAcquire()
        {
            // FIX #4: Check disposed BEFORE incrementing
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

            // FIX #5: Prevent overflow
            if (newCount <= 0) // Overflow detection
            {
                _ = Interlocked.Decrement(ref _activeUsers);
                s_logger?.Error($"[NW.{nameof(ConcurrencyGate)}:Entry] activeUsers overflow detected");
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

            // FIX #6: Detect underflow
            if (remaining < 0)
            {
                s_logger?.Error($"[NW.{nameof(ConcurrencyGate)}:Entry] activeUsers underflow detected");
                _ = Interlocked.Exchange(ref _activeUsers, 0);
            }
        }

        /// <summary>
        /// Attempts to increment queue count if under limit.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryIncrementQueue()
        {
            if (QueueMax == int.MaxValue)
            {
                _ = Interlocked.Increment(ref _queueCount);
                return true;
            }

            // Spin-loop CAS for atomic check-and-increment
            while (true)
            {
                int current = Volatile.Read(ref _queueCount);

                if (current >= QueueMax)
                {
                    return false;
                }

                int original = Interlocked.CompareExchange(
                    ref _queueCount,
                    current + 1,
                    current);

                if (original == current)
                {
                    return true; // Success
                }

                // FIX #7: Add spin-wait to reduce contention
                Thread.SpinWait(1);
            }
        }

        /// <summary>
        /// Decrements queue count.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DecrementQueue()
        {
            int remaining = Interlocked.Decrement(ref _queueCount);

            // FIX #8: Detect underflow
            if (remaining < 0)
            {
                s_logger?.Error($"[NW.{nameof(ConcurrencyGate)}:Entry] queueCount underflow detected");
                _ = Interlocked.Exchange(ref _queueCount, 0);
            }
        }

        /// <summary>
        /// Safely disposes the semaphore after waiting for active users.
        /// </summary>
        public void Dispose()
        {
            // FIX #9: Use lock to prevent concurrent disposal and usage
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

                //  FIX #10: Log if forced disposal with active users
                int remainingUsers = Volatile.Read(ref _activeUsers);
                if (remainingUsers > 0)
                {
                    s_logger?.Warn(
                        $"[NW.{nameof(ConcurrencyGate)}:Entry] disposing with {remainingUsers} active users");
                }

                // Dispose semaphore
                try
                {
                    Sem.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // Already disposed - acceptable
                }
                catch (Exception ex)
                {
                    s_logger?.Error($"[NW.{nameof(ConcurrencyGate)}:Entry] disposal-error", ex);
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
            catch (Exception ex)
            {
                s_logger?.Error($"[NW.{nameof(ConcurrencyGate)}:Lease] release-error", ex);
            }
            finally
            {
                _entry.Release();
            }
        }
    }

    #endregion Lease Struct

    #region Public API

    /// <summary>
    /// Attempts to enter immediately without waiting.
    /// </summary>
    /// <param name="opcode"></param>
    /// <param name="attr"></param>
    /// <param name="lease"></param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public bool TryEnter(
        ushort opcode,
        PacketConcurrencyLimitAttribute attr,
        out Lease lease)
    {
        // FIX #12: Check and reset circuit breaker
        if (IS_CIRCUIT_OPEN())
        {
            _ = Interlocked.Increment(ref _circuitBreakerTrips);
            lease = default;
            return false;
        }

        VALIDATE_ATTRIBUTE(attr);

        Entry entry = GET_OR_CREATE_ENTRY(opcode, attr);

        if (!entry.TryAcquire())
        {
            _ = Interlocked.Increment(ref _totalRejected);
            lease = default;
            return false;
        }

        try
        {
            if (entry.Sem.Wait(0))
            {
                entry.Touch();
                _ = Interlocked.Increment(ref _totalAcquired);

                lease = new Lease(entry.Sem, entry);
                return true;
            }

            _ = Interlocked.Increment(ref _totalRejected);
            lease = default;
            return false;
        }
        catch (ObjectDisposedException)
        {
            // Entry was disposed - treat as rejection
            _ = Interlocked.Increment(ref _totalRejected);
            lease = default;
            return false;
        }
        catch (Exception ex)
        {
            s_logger?.Error($"[NW.{nameof(ConcurrencyGate)}:{nameof(TryEnter)}] unexpected error opcode={opcode:X4}", ex);
            lease = default;
            return false;
        }
        finally
        {
            entry.Release();
        }
    }

    /// <summary>
    /// Enters with optional waiting when queuing is enabled.
    /// </summary>
    /// <param name="opcode"></param>
    /// <param name="attr"></param>
    /// <param name="ct"></param>
    /// <exception cref="ConcurrencyConflictException"></exception>
    /// <exception cref="TimeoutException"></exception>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public async ValueTask<Lease> EnterAsync(
        ushort opcode,
        PacketConcurrencyLimitAttribute attr,
        CancellationToken ct = default)
    {
        VALIDATE_ATTRIBUTE(attr);

        // FIX #13: Create timeout CTS properly
        using CancellationTokenSource timeoutCts = new();
        timeoutCts.CancelAfter(_timeout);

        using CancellationTokenSource linkedCts =
            CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        Entry entry = GET_OR_CREATE_ENTRY(opcode, attr);

        if (!entry.TryAcquire())
        {
            throw new ConcurrencyConflictException(
                $"Entry for opcode {opcode:X4} is being disposed");
        }

        try
        {
            // No queue: immediate attempt only
            if (!entry.Queue)
            {
                if (!entry.Sem.Wait(0, linkedCts.Token))
                {
                    _ = Interlocked.Increment(ref _totalRejected);
                    throw new ConcurrencyConflictException(
                        $"Concurrency limit reached for opcode {opcode:X4} (no queue)");
                }

                entry.Touch();
                _ = Interlocked.Increment(ref _totalAcquired);

                return new Lease(entry.Sem, entry);
            }

            // Queue enabled
            return await ENTER_WITH_QUEUE_ASYNC(entry, opcode, linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            _ = Interlocked.Increment(ref _totalRejected);
            throw new TimeoutException(
                $"Concurrency gate timeout after {_timeout.TotalSeconds}s for opcode {opcode:X4}");
        }
        catch
        {
            entry.Release();
            throw;
        }
    }

    /// <summary>
    /// Gets diagnostic statistics.
    /// </summary>
    public (
        long TotalAcquired,
        long TotalRejected,
        long TotalQueued,
        long TotalCleaned,
        long CircuitBreakerTrips,
        bool CircuitBreakerOpen,
        int TrackedOpcodes
    ) GetStatistics()
    {
        return (
            Interlocked.Read(ref _totalAcquired),
            Interlocked.Read(ref _totalRejected),
            Interlocked.Read(ref _totalQueued),
            Interlocked.Read(ref _totalCleanedEntries),
            Interlocked.Read(ref _circuitBreakerTrips),
            Volatile.Read(ref _circuitBreakerOpen) == 1,
            s_table.Count
        );
    }

    /// <summary>
    /// Resets statistics. For testing only.
    /// </summary>
    internal void ResetStatistics()
    {
        _ = Interlocked.Exchange(ref _totalAcquired, 0);
        _ = Interlocked.Exchange(ref _totalRejected, 0);
        _ = Interlocked.Exchange(ref _totalQueued, 0);
        _ = Interlocked.Exchange(ref _totalCleanedEntries, 0);
        _ = Interlocked.Exchange(ref _circuitBreakerTrips, 0);
        _ = Interlocked.Exchange(ref _circuitBreakerOpen, 0);
    }

    #endregion Public API

    #region Report Generation

    /// <summary>
    /// Generates a human-readable diagnostic report of the concurrency gate state.
    /// </summary>
    [StackTraceHidden]
    public string GenerateReport()
    {
        // Take snapshot
        List<KeyValuePair<ushort, Entry>> snapshot =
            [.. s_table];

        // Sort by load (highest pressure first)
        snapshot.Sort((a, b) =>
        {
            int aPressure = a.Value.Capacity - a.Value.Sem.CurrentCount;
            int bPressure = b.Value.Capacity - b.Value.Sem.CurrentCount;

            int cmp = bPressure.CompareTo(aPressure);
            return cmp != 0 ? cmp : b.Value.QueueCount.CompareTo(a.Value.QueueCount);
        });

        // Calculate metrics
        (long TotalAcquired, long TotalRejected, long TotalQueued, long TotalCleaned, long CircuitBreakerTrips, bool CircuitBreakerOpen, int TrackedOpcodes) stats = GetStatistics();
        double rejectionRate = 0.0;
        long totalAttempts = stats.TotalAcquired + stats.TotalRejected;
        if (totalAttempts > 0)
        {
            rejectionRate = stats.TotalRejected * 100.0 / totalAttempts;
        }

        // Build report
        StringBuilder sb = new();

        APPEND_REPORT_HEADER(sb, stats, rejectionRate);
        APPEND_OPCODE_DETAILS(sb, snapshot);

        return sb.ToString();
    }

    private void APPEND_REPORT_HEADER(
        StringBuilder sb,
        (long TotalAcquired, long TotalRejected, long TotalQueued,
         long TotalCleaned, long CircuitBreakerTrips, bool CircuitBreakerOpen,
         int TrackedOpcodes) stats,
        double rejectionRate)
    {
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] ConcurrencyGate Status:");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"CleanupInterval    : {CleanupInterval.TotalMinutes:F1} min");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"MinIdleAge         : {MinIdleAge.TotalMinutes:F1} min");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"TrackedOpcodes     : {stats.TrackedOpcodes}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"TotalAcquired      : {stats.TotalAcquired:N0}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"TotalRejected      : {stats.TotalRejected:N0}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"TotalQueued        : {stats.TotalQueued:N0}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"TotalCleaned       : {stats.TotalCleaned:N0}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"RejectionRate      : {rejectionRate:F2}%");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"CircuitBreaker     : {(stats.CircuitBreakerOpen ? "OPEN" : "Closed")} (trips={stats.CircuitBreakerTrips})");
        _ = sb.AppendLine();
    }

    private static void APPEND_OPCODE_DETAILS(
        StringBuilder sb,
        List<KeyValuePair<ushort, Entry>> snapshot)
    {
        _ = sb.AppendLine("Top Opcodes by Load:");
        _ = sb.AppendLine("---------------------------------------------------------------------------------");
        _ = sb.AppendLine("Opcode | Capacity | InUse | Avail | Queue | QueueMax | Queuing | LastUsed");
        _ = sb.AppendLine("---------------------------------------------------------------------------------");

        if (snapshot.Count == 0)
        {
            _ = sb.AppendLine("(no tracked opcodes)");
        }
        else
        {
            APPEND_TOP_OPCODES(sb, snapshot, maxRows: 50);
        }

        _ = sb.AppendLine("---------------------------------------------------------------------------------");
    }

    private static void APPEND_TOP_OPCODES(
        StringBuilder sb,
        List<KeyValuePair<ushort, Entry>> snapshot,
        int maxRows)
    {
        int rows = 0;

        foreach (KeyValuePair<ushort, Entry> kvp in snapshot)
        {
            if (rows++ >= maxRows)
            {
                break;
            }

            ushort opcode = kvp.Key;
            Entry entry = kvp.Value;

            int available = entry.Sem.CurrentCount;
            int inUse = entry.Capacity - available;
            int queueCount = entry.QueueCount;
            string queueEnabled = entry.Queue ? "yes" : " no";
            string queueMaxStr = entry.QueueMax == int.MaxValue ? "∞" : entry.QueueMax.ToString(CultureInfo.InvariantCulture);
            DateTimeOffset lastUsed = entry.LastUsedUtc;

            _ = sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"0x{opcode:X4} | " +
                $"{entry.Capacity,8} | " +
                $"{inUse,5} | " +
                $"{available,5} | " +
                $"{queueCount,5} | " +
                $"{queueMaxStr,8} | " +
                $"{queueEnabled,7} | " +
                $"{lastUsed:HH:mm:ss}");
        }
    }

    #endregion Report Generation

    #region Private Methods

    /// <summary>
    /// Checks if circuit breaker is open and attempts to close if timeout expired.
    /// </summary>
    private bool IS_CIRCUIT_OPEN()
    {
        // Check if already open
        if (Volatile.Read(ref _circuitBreakerOpen) == 1)
        {
            // Try to close if reset time passed
            long resetTimeTicks = Volatile.Read(ref _circuitBreakerResetTimeTicks);
            long nowTicks = DateTime.UtcNow.Ticks;

            if (nowTicks >= resetTimeTicks && Interlocked.CompareExchange(ref _circuitBreakerOpen, 0, 1) == 1)
            {
                // Reset counters
                _ = Interlocked.Exchange(ref _totalAcquired, 0);
                _ = Interlocked.Exchange(ref _totalRejected, 0);

                s_logger?.Info($"[NW.{nameof(ConcurrencyGate)}] circuit breaker closed");
            }

            return Volatile.Read(ref _circuitBreakerOpen) == 1;
        }

        // Check if should open
        long totalAttempts = Volatile.Read(ref _totalAcquired) +
                                     Volatile.Read(ref _totalRejected);

        if (totalAttempts < CircuitBreakerMinSamples)
        {
            return false;
        }

        double rejectionRate = (double)Volatile.Read(ref _totalRejected) / totalAttempts;

        if (rejectionRate > CircuitBreakerThreshold)
        {
            if (Interlocked.CompareExchange(ref _circuitBreakerOpen, 1, 0) == 0)
            {
                long resetTime = DateTime.UtcNow.AddSeconds(CircuitBreakerResetAfterSeconds).Ticks;
                _ = Interlocked.Exchange(ref _circuitBreakerResetTimeTicks, resetTime);

                s_logger?.Error(
                    $"[NW.{nameof(ConcurrencyGate)}] circuit breaker opened " +
                    $"(rejection_rate={rejectionRate:P2}, attempts={totalAttempts})");
            }

            return true;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void VALIDATE_ATTRIBUTE(PacketConcurrencyLimitAttribute attr)
    {
        ArgumentNullException.ThrowIfNull(attr);

        if (attr.Max <= 0)
        {
            throw new ArgumentException(
                $"Concurrency max must be > 0, got {attr.Max}",
                nameof(attr));
        }

        if (attr.QueueMax < 0)
        {
            throw new ArgumentException(
                $"Queue max cannot be negative, got {attr.QueueMax}",
                nameof(attr));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Entry GET_OR_CREATE_ENTRY(
        ushort opcode,
        PacketConcurrencyLimitAttribute attr)
    {
        return s_table.GetOrAdd(
            opcode,
            _ => new Entry(attr.Max, attr.Queue, attr.QueueMax));
    }

    private async ValueTask<Lease> ENTER_WITH_QUEUE_ASYNC(
        Entry entry,
        ushort opcode,
        CancellationToken ct)
    {
        if (!entry.TryIncrementQueue())
        {
            _ = Interlocked.Increment(ref _totalRejected);
            throw new ConcurrencyConflictException(
                $"Concurrency queue is full for opcode {opcode:X4} " +
                $"(limit={entry.QueueMax}, current={entry.QueueCount})");
        }

        _ = Interlocked.Increment(ref _totalQueued);

        try
        {
            await entry.Sem.WaitAsync(ct).ConfigureAwait(false);

            entry.Touch();
            _ = Interlocked.Increment(ref _totalAcquired);

            return new Lease(entry.Sem, entry);
        }
        catch (OperationCanceledException)
        {
            _ = Interlocked.Increment(ref _totalRejected);
            throw;
        }
        finally
        {
            entry.DecrementQueue();
        }
    }

    [StackTraceHidden]
    private void CLEANUP_IDLE_ENTRIES()
    {
        try
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            int removed = 0;

            foreach (KeyValuePair<ushort, Entry> kvp in s_table)
            {
                ushort opcode = kvp.Key;
                Entry entry = kvp.Value;

                if (!entry.IsIdle)
                {
                    continue;
                }

                TimeSpan age = now - entry.LastUsedUtc;
                if (age < MinIdleAge)
                {
                    continue;
                }

                // Remove before disposal to prevent new usage
                if (s_table.TryRemove(opcode, out Entry? removedEntry)
                    && removedEntry is not null)
                {
                    removedEntry.Dispose();
                    removed++;
                    _ = Interlocked.Increment(ref _totalCleanedEntries);
                }
            }

            if (removed > 0)
            {
                s_logger?.Debug($"[NW.{nameof(ConcurrencyGate)}] cleanup removed={removed} remaining={s_table.Count}");
            }
        }
        catch (Exception ex)
        {
            s_logger?.Error($"[NW.{nameof(ConcurrencyGate)}] cleanup-error", ex);
        }
    }

    #endregion Private Methods
}
