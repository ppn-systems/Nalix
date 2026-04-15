// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nalix.Common.Abstractions;
using Nalix.Common.Exceptions;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;

namespace Nalix.Network.Pipeline.Throttling;

/// <summary>
/// High-performance per-opcode concurrency limiter with optional FIFO queuing.
/// Thread-safe with reference counting for safe disposal.
/// Automatically cleans up idle entries to prevent memory leaks.
/// </summary>
[DebuggerNonUserCode]
[SkipLocalsInit]
public sealed class ConcurrencyGate : IReportable, IWithLogging<ConcurrencyGate>
{
    #region Constants

    private const double CircuitBreakerThreshold = 0.95;
    private const int CircuitBreakerMinSamples = 1000;
    private const int CircuitBreakerResetAfterSeconds = 60;

    private readonly TimeSpan _minIdleAge = TimeSpan.FromMinutes(10);
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(1);

    #endregion Constants

    #region Fields

    private readonly System.Collections.Concurrent.ConcurrentDictionary<ushort, Entry> _table = new();
    private ILogger? _logger;

    private long _totalAcquired;
    private long _totalRejected;
    private long _totalQueued;
    private long _totalCleanedEntries;
    private long _circuitBreakerTrips;
    private readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

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
                name: $"concurrency.gate.cleanup.{this.GetHashCode():X8}",
                interval: _cleanupInterval,
                work: _ =>
                {
                    this.CLEANUP_IDLE_ENTRIES();
                    return ValueTask.CompletedTask;
                },
                options: new RecurringOptions
                {
                    NonReentrant = true,
                    Tag = TaskNaming.Tags.Service,
                    Jitter = TimeSpan.FromSeconds(10),
                    ExecutionTimeout = TimeSpan.FromSeconds(5)
                });
        }
        catch (Exception ex)
        {
            throw new InternalErrorException($"[NW.{nameof(ConcurrencyGate)}] initialization-error: {ex.Message}", ex);
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
                this.Logger?.Error($"[NW.{nameof(ConcurrencyGate)}:Entry] activeUsers overflow detected");
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
                this.Logger?.Error($"[NW.{nameof(ConcurrencyGate)}:Entry] activeUsers underflow detected");
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
                this.Logger?.Error($"[NW.{nameof(ConcurrencyGate)}:Entry] queueCount underflow detected");
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
                    this.Logger?.Warn(
                        $"[NW.{nameof(ConcurrencyGate)}:Entry] disposing with {remainingUsers} active users");
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
                catch (Exception ex)
                {
                    this.Logger?.Error($"[NW.{nameof(ConcurrencyGate)}:Entry] disposal-error", ex);
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
                _entry.Logger?.Error($"[NW.{nameof(ConcurrencyGate)}:Lease] release-error", ex);
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
    /// Assigns a logger instance used by the gate for diagnostic output.
    /// </summary>
    /// <param name="logger">The logger to use for subsequent diagnostics.</param>
    /// <returns>The current <see cref="ConcurrencyGate"/> instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ConcurrencyGate WithLogging(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        return this;
    }

    /// <summary>
    /// Attempts to enter immediately without waiting.
    /// </summary>
    /// <param name="opcode"></param>
    /// <param name="attr"></param>
    /// <param name="lease"></param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public bool TryEnter(ushort opcode, PacketConcurrencyLimitAttribute attr, out Lease lease)
    {
        if (this.IS_CIRCUIT_OPEN())
        {
            _ = Interlocked.Increment(ref _circuitBreakerTrips);
            lease = default;
            return false;
        }

        VALIDATE_ATTRIBUTE(attr);

        Entry entry = this.GET_OR_CREATE_ENTRY(opcode, attr);

        if (!entry.TryAcquire())
        {
            _ = Interlocked.Increment(ref _totalRejected);
            lease = default;
            return false;
        }

        bool leaseGranted = false;
        try
        {
            if (entry.Sem.Wait(0))
            {
                entry.Touch();
                _ = Interlocked.Increment(ref _totalAcquired);

                lease = new Lease(entry.Sem, entry);
                leaseGranted = true;
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
            _logger?.Error($"[NW.{nameof(ConcurrencyGate)}:{nameof(TryEnter)}] unexpected error opcode={opcode:X4}", ex);
            lease = default;
            return false;
        }
        finally
        {
            if (!leaseGranted)
            {
                entry.Release();
            }
        }
    }

    /// <summary>
    /// Enters with optional waiting when queuing is enabled.
    /// </summary>
    /// <param name="opcode"></param>
    /// <param name="attr"></param>
    /// <param name="ct"></param>
    /// <exception cref="ConcurrencyFailureException"></exception>
    /// <exception cref="TimeoutException"></exception>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public async ValueTask<Lease> EnterAsync(ushort opcode, PacketConcurrencyLimitAttribute attr, CancellationToken ct = default)
    {
        if (this.IS_CIRCUIT_OPEN())
        {
            _ = Interlocked.Increment(ref _circuitBreakerTrips);
            throw new ConcurrencyFailureException(
                $"Circuit breaker is open for opcode {opcode:X4}");
        }

        VALIDATE_ATTRIBUTE(attr);

        Entry entry = this.GET_OR_CREATE_ENTRY(opcode, attr);

        if (!entry.TryAcquire())
        {
            throw new ConcurrencyFailureException(
                $"Entry for opcode {opcode:X4} is being disposed");
        }

        try
        {
            // No queue: immediate attempt only
            if (!entry.Queue)
            {
                if (!entry.Sem.Wait(0, ct))
                {
                    _ = Interlocked.Increment(ref _totalRejected);
                    throw new ConcurrencyFailureException(
                        $"Concurrency limit reached for opcode {opcode:X4} (no queue)");
                }

                entry.Touch();
                _ = Interlocked.Increment(ref _totalAcquired);

                return new Lease(entry.Sem, entry);
            }

            // Queue enabled
            return await this.ENTER_WITH_QUEUE_ASYNC(entry, opcode, _timeout, ct).ConfigureAwait(false);
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
    public (long TotalAcquired, long TotalRejected, long TotalQueued, long TotalCleaned, long CircuitBreakerTrips, bool CircuitBreakerOpen, int TrackedOpcodes) GetStatistics()
    {
        return (
            Interlocked.Read(ref _totalAcquired),
            Interlocked.Read(ref _totalRejected),
            Interlocked.Read(ref _totalQueued),
            Interlocked.Read(ref _totalCleanedEntries),
            Interlocked.Read(ref _circuitBreakerTrips),
            Volatile.Read(ref _circuitBreakerOpen) == 1,
            _table.Count
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
            [.. _table];

        // Sort by load (highest pressure first)
        snapshot.Sort((a, b) =>
        {
            int aPressure = a.Value.Capacity - a.Value.Sem.CurrentCount;
            int bPressure = b.Value.Capacity - b.Value.Sem.CurrentCount;

            int cmp = bPressure.CompareTo(aPressure);
            return cmp != 0 ? cmp : b.Value.QueueCount.CompareTo(a.Value.QueueCount);
        });

        // Calculate metrics
        double rejectionRate = 0.0;
        long totalAttempts = Interlocked.Read(ref _totalAcquired) + Interlocked.Read(ref _totalRejected);
        if (totalAttempts > 0)
        {
            rejectionRate = Interlocked.Read(ref _totalRejected) * 100.0 / totalAttempts;
        }

        // Build report
        StringBuilder sb = new();

        this.APPEND_REPORT_HEADER(sb, rejectionRate);
        APPEND_OPCODE_DETAILS(sb, snapshot);

        return sb.ToString();
    }

    /// <summary>
    /// Generates a key-value diagnostic summary of the concurrency gate and per-opcode state.
    /// </summary>
    public IDictionary<string, object> GetReportData()
    {
        List<KeyValuePair<ushort, Entry>> entries = [.. _table];
        entries.Sort((a, b) =>
        {
            int aBusy = a.Value.Capacity - a.Value.Sem.CurrentCount;
            int bBusy = b.Value.Capacity - b.Value.Sem.CurrentCount;
            int cmp = bBusy.CompareTo(aBusy);
            return cmp != 0 ? cmp : b.Value.QueueCount.CompareTo(a.Value.QueueCount);
        });

        long totalAttempts = Interlocked.Read(ref _totalAcquired) + Interlocked.Read(ref _totalRejected);
        double rejectionRate = totalAttempts > 0 ? (Interlocked.Read(ref _totalRejected) * 100.0 / totalAttempts) : 0.0;

        Dictionary<string, object> report = new()
        {
            ["UtcNow"] = DateTime.UtcNow,
            ["CleanupIntervalMinutes"] = _cleanupInterval.TotalMinutes,
            ["MinIdleAgeMinutes"] = _minIdleAge.TotalMinutes,
            ["TrackedOpcodes"] = _table.Count,
            ["TotalAcquired"] = Interlocked.Read(ref _totalAcquired),
            ["TotalRejected"] = Interlocked.Read(ref _totalRejected),
            ["TotalQueued"] = Interlocked.Read(ref _totalQueued),
            ["TotalCleaned"] = Interlocked.Read(ref _totalCleanedEntries),
            ["RejectionRate"] = rejectionRate,
            ["CircuitBreaker"] = new Dictionary<string, object>
            {
                ["IsOpen"] = Volatile.Read(ref _circuitBreakerOpen) == 1,
                ["Trips"] = Interlocked.Read(ref _circuitBreakerTrips)
            }
        };

        report["Opcodes"] = entries.Take(50).Select(kvp =>
        {
            ushort opcode = kvp.Key;
            Entry entry = kvp.Value;
            int available = entry.Sem.CurrentCount;
            int inUse = entry.Capacity - available;
            string queueMaxStr = entry.QueueMax == int.MaxValue ? "∞" : entry.QueueMax.ToString(CultureInfo.InvariantCulture);

            return new Dictionary<string, object>
            {
                ["Opcode"] = $"0x{opcode:X4}",
                ["Capacity"] = entry.Capacity,
                ["InUse"] = inUse,
                ["Available"] = available,
                ["Queuing"] = entry.Queue,
                ["QueueCount"] = entry.QueueCount,
                ["QueueMax"] = queueMaxStr,
                ["IsIdle"] = entry.IsIdle,
                ["LastUsedUtc"] = entry.LastUsedUtc
            };
        }).ToList();

        return report;
    }

    private void APPEND_REPORT_HEADER(StringBuilder sb, double rejectionRate)
    {
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] ConcurrencyGate Status:");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"CleanupInterval    : {_cleanupInterval.TotalMinutes:F1} min");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"MinIdleAge         : {_minIdleAge.TotalMinutes:F1} min");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"TrackedOpcodes     : {_table.Count}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"TotalAcquired      : {Interlocked.Read(ref _totalAcquired):N0}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"TotalRejected      : {Interlocked.Read(ref _totalRejected):N0}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"TotalQueued        : {Interlocked.Read(ref _totalQueued):N0}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"TotalCleaned       : {Interlocked.Read(ref _totalCleanedEntries):N0}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"RejectionRate      : {rejectionRate:F2}%");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"CircuitBreaker     : {(Volatile.Read(ref _circuitBreakerOpen) == 1 ? "OPEN" : "Closed")} (trips={Interlocked.Read(ref _circuitBreakerTrips)})");
        _ = sb.AppendLine();
    }

    private static void APPEND_OPCODE_DETAILS(StringBuilder sb, List<KeyValuePair<ushort, Entry>> snapshot)
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

    private static void APPEND_TOP_OPCODES(StringBuilder sb, List<KeyValuePair<ushort, Entry>> snapshot, int maxRows)
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

                _logger?.Info($"[NW.{nameof(ConcurrencyGate)}] circuit breaker closed");
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

                _logger?.Error(
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

    private Entry GET_OR_CREATE_ENTRY(
        ushort opcode,
        PacketConcurrencyLimitAttribute attr)
    {
        return _table.GetOrAdd(
            opcode,
            _ => new Entry(attr.Max, attr.Queue, attr.QueueMax, _logger));
    }

    private async ValueTask<Lease> ENTER_WITH_QUEUE_ASYNC(
        Entry entry,
        ushort opcode,
        TimeSpan timeout,
        CancellationToken ct)
    {
        if (!entry.TryIncrementQueue())
        {
            _ = Interlocked.Increment(ref _totalRejected);
            throw new ConcurrencyFailureException(
                $"Concurrency queue is full for opcode {opcode:X4} " +
                $"(limit={entry.QueueMax}, current={entry.QueueCount})");
        }

        _ = Interlocked.Increment(ref _totalQueued);

        try
        {
            bool acquired = await entry.Sem.WaitAsync(timeout, ct).ConfigureAwait(false);
            if (!acquired)
            {
                _ = Interlocked.Increment(ref _totalRejected);
                throw new TimeoutException(
                    $"Concurrency gate timeout after {timeout.TotalSeconds}s for opcode {opcode:X4}");
            }

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

            foreach (KeyValuePair<ushort, Entry> kvp in _table)
            {
                ushort opcode = kvp.Key;
                Entry entry = kvp.Value;

                if (!entry.IsIdle)
                {
                    continue;
                }

                TimeSpan age = now - entry.LastUsedUtc;
                if (age < _minIdleAge)
                {
                    continue;
                }

                // Remove before disposal to prevent new usage
                if (_table.TryRemove(opcode, out Entry? removedEntry)
                    && removedEntry is not null)
                {
                    removedEntry.Dispose();
                    removed++;
                    _ = Interlocked.Increment(ref _totalCleanedEntries);
                }
            }

            if (removed > 0)
            {
                _logger?.Debug($"[NW.{nameof(ConcurrencyGate)}] cleanup removed={removed} remaining={_table.Count}");
            }
        }
        catch (Exception ex)
        {
            _logger?.Error($"[NW.{nameof(ConcurrencyGate)}] cleanup-error", ex);
        }
    }

    #endregion Private Methods
}
