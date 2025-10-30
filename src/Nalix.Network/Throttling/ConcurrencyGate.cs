// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Diagnostics.Abstractions;
using Nalix.Common.Exceptions;
using Nalix.Common.Networking.Packets.Attributes;
using Nalix.Common.Shared.Abstractions;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;

namespace Nalix.Network.Throttling;

/// <summary>
/// High-performance per-opcode concurrency limiter with optional FIFO queuing.
/// Thread-safe with reference counting for safe disposal.
/// Automatically cleans up idle entries to prevent memory leaks.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
[System.Runtime.CompilerServices.SkipLocalsInit]
public sealed class ConcurrencyGate : IReportable
{
    #region Constants

    private const System.Double CircuitBreakerThreshold = 0.95;
    private const System.Int32 CircuitBreakerMinSamples = 1000;
    private const System.Int32 CircuitBreakerResetAfterSeconds = 60;

    private readonly System.TimeSpan MinIdleAge = System.TimeSpan.FromMinutes(10);
    private readonly System.TimeSpan CleanupInterval = System.TimeSpan.FromMinutes(1);

    #endregion Constants

    #region Fields

    private readonly System.Collections.Concurrent.ConcurrentDictionary<System.UInt16, Entry> s_table = new();

    [System.Diagnostics.CodeAnalysis.AllowNull]
    private static readonly ILogger s_logger = InstanceManager.Instance.GetExistingInstance<ILogger>();
    private readonly System.TimeSpan s_timeout = System.TimeSpan.FromSeconds(20);

    private System.Int64 s_totalAcquired;
    private System.Int64 s_totalRejected;
    private System.Int64 s_totalQueued;
    private System.Int64 s_totalCleanedEntries;
    private System.Int64 s_circuitBreakerTrips;

    // FIX #1: Circuit breaker state management
    private System.Int32 s_circuitBreakerOpen; // 0 = closed, 1 = open
    private System.Int64 s_circuitBreakerResetTimeTicks;

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
                    return System.Threading.Tasks.ValueTask.CompletedTask;
                },
                options: new RecurringOptions
                {
                    NonReentrant = true,
                    Tag = TaskNaming.Tags.Service,
                    Jitter = System.TimeSpan.FromSeconds(10),
                    ExecutionTimeout = System.TimeSpan.FromSeconds(5)
                });

            s_logger?.Debug($"[NW.{nameof(ConcurrencyGate)}] initialized with cleanup interval={CleanupInterval.TotalMinutes:F1}min");
        }
        catch (System.Exception ex)
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
    public sealed class Entry : System.IDisposable
    {
        private System.Int32 _queueCount;
        private System.Int32 _activeUsers; // Reference count
        private System.Int64 _lastUsedUtcTicks;
        private System.Int32 _disposed;

        // FIX #2: Add lock for disposal coordination
        private readonly System.Threading.Lock _disposalLock = new();

        /// <summary>
        /// Gets a value indicating whether FIFO queuing is enabled for this entry.
        /// </summary>
        public System.Boolean Queue { get; }

        /// <summary>
        /// Gets the maximum number of concurrent operations allowed for this entry.
        /// </summary>
        public System.Int32 Capacity { get; }

        /// <summary>
        /// Gets the maximum number of operations that can be queued for this entry.
        /// </summary>
        public System.Int32 QueueMax { get; }

        /// <summary>
        /// Gets the semaphore used to enforce the concurrency limit for this entry.
        /// </summary>
        public System.Threading.SemaphoreSlim Sem { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Entry"/> class.
        /// </summary>
        public Entry(System.Int32 max, System.Boolean queue, System.Int32 queueMax)
        {
            if (max <= 0)
            {
                throw new System.ArgumentOutOfRangeException(nameof(max), "Capacity must be positive");
            }

            Queue = queue;
            Capacity = max;
            QueueMax = queueMax < 0 ? System.Int32.MaxValue : queueMax;
            Sem = new System.Threading.SemaphoreSlim(Capacity, Capacity);

            _activeUsers = 0;
            _queueCount = 0;
            _disposed = 0;

            Touch();
        }

        /// <summary>
        /// Updates last used timestamp.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Touch()
        {
            System.Int64 nowTicks = System.DateTimeOffset.UtcNow.UtcDateTime.Ticks;
            System.Threading.Interlocked.Exchange(ref _lastUsedUtcTicks, nowTicks);
        }

        /// <summary>
        /// Gets the last used timestamp.
        /// </summary>
        public System.DateTimeOffset LastUsedUtc
        {
            get
            {
                System.Int64 ticks = System.Threading.Interlocked.Read(ref _lastUsedUtcTicks);
                return new System.DateTimeOffset(ticks, System.TimeSpan.Zero);
            }
        }

        /// <summary>
        /// Gets current queue count.
        /// </summary>
        public System.Int32 QueueCount => System.Threading.Volatile.Read(ref _queueCount);

        /// <summary>
        /// Entry is idle when no slots are in use and queue is empty.
        /// </summary>
        public System.Boolean IsIdle
        {
            get
            {
                if (System.Threading.Volatile.Read(ref _disposed) != 0)
                {
                    return false;
                }

                System.Int32 activeUsers = System.Threading.Volatile.Read(ref _activeUsers);
                System.Int32 queueCount = System.Threading.Volatile.Read(ref _queueCount);

                // FIX #3: Use SpinLock for atomic read of semaphore state
                System.Int32 available = Sem.CurrentCount;

                return activeUsers == 0 && available == Capacity && queueCount == 0;
            }
        }

        /// <summary>
        /// Attempts to acquire usage reference. Returns false if disposed.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public System.Boolean TryAcquire()
        {
            // FIX #4: Check disposed BEFORE incrementing
            if (System.Threading.Volatile.Read(ref _disposed) != 0)
            {
                return false;
            }

            System.Int32 newCount = System.Threading.Interlocked.Increment(ref _activeUsers);

            // Double-check after increment
            if (System.Threading.Volatile.Read(ref _disposed) != 0)
            {
                System.Threading.Interlocked.Decrement(ref _activeUsers);
                return false;
            }

            // FIX #5: Prevent overflow
            if (newCount <= 0) // Overflow detection
            {
                System.Threading.Interlocked.Decrement(ref _activeUsers);
                s_logger?.Error($"[NW.{nameof(ConcurrencyGate)}:Entry] activeUsers overflow detected");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Releases usage reference.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Release()
        {
            System.Int32 remaining = System.Threading.Interlocked.Decrement(ref _activeUsers);

            // FIX #6: Detect underflow
            if (remaining < 0)
            {
                s_logger?.Error($"[NW.{nameof(ConcurrencyGate)}:Entry] activeUsers underflow detected");
                System.Threading.Interlocked.Exchange(ref _activeUsers, 0);
            }
        }

        /// <summary>
        /// Attempts to increment queue count if under limit.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public System.Boolean TryIncrementQueue()
        {
            if (QueueMax == System.Int32.MaxValue)
            {
                System.Threading.Interlocked.Increment(ref _queueCount);
                return true;
            }

            // Spin-loop CAS for atomic check-and-increment
            while (true)
            {
                System.Int32 current = System.Threading.Volatile.Read(ref _queueCount);

                if (current >= QueueMax)
                {
                    return false;
                }

                System.Int32 original = System.Threading.Interlocked.CompareExchange(
                    ref _queueCount,
                    current + 1,
                    current);

                if (original == current)
                {
                    return true; // Success
                }

                // FIX #7: Add spin-wait to reduce contention
                System.Threading.Thread.SpinWait(1);
            }
        }

        /// <summary>
        /// Decrements queue count.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void DecrementQueue()
        {
            System.Int32 remaining = System.Threading.Interlocked.Decrement(ref _queueCount);

            // FIX #8: Detect underflow
            if (remaining < 0)
            {
                s_logger?.Error($"[NW.{nameof(ConcurrencyGate)}:Entry] queueCount underflow detected");
                System.Threading.Interlocked.Exchange(ref _queueCount, 0);
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
                if (System.Threading.Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
                {
                    return; // Already disposed
                }

                // Wait for active users with exponential backoff
                System.Int32 waitedMs = 0;
                System.Int32 backoffMs = 1;
                const System.Int32 maxWaitMs = 500;
                const System.Int32 maxBackoffMs = 50;

                while (System.Threading.Volatile.Read(ref _activeUsers) > 0 && waitedMs < maxWaitMs)
                {
                    System.Threading.Thread.Sleep(backoffMs);
                    waitedMs += backoffMs;
                    backoffMs = System.Math.Min(backoffMs * 2, maxBackoffMs);
                }

                //  FIX #10: Log if forced disposal with active users
                System.Int32 remainingUsers = System.Threading.Volatile.Read(ref _activeUsers);
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
                catch (System.ObjectDisposedException)
                {
                    // Already disposed - acceptable
                }
                catch (System.Exception ex)
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
    public readonly struct Lease(System.Threading.SemaphoreSlim sem, ConcurrencyGate.Entry entry) : System.IDisposable
    {
        private readonly Entry _entry = entry ?? throw new System.ArgumentNullException(nameof(entry));
        private readonly System.Threading.SemaphoreSlim _sem = sem ?? throw new System.ArgumentNullException(nameof(sem));

        /// <summary>
        /// Releases the concurrency slot.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            if (_sem is null || _entry is null)
            {
                return;
            }

            try
            {
                _sem.Release();
            }
            catch (System.ObjectDisposedException)
            {
                // Semaphore was disposed during cleanup - acceptable
            }
            catch (System.Exception ex)
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Boolean TryEnter(
        System.UInt16 opcode,
        [System.Diagnostics.CodeAnalysis.NotNull] PacketConcurrencyLimitAttribute attr,
        out Lease lease)
    {
        // FIX #12: Check and reset circuit breaker
        if (IS_CIRCUIT_OPEN())
        {
            System.Threading.Interlocked.Increment(ref s_circuitBreakerTrips);
            lease = default;
            return false;
        }

        VALIDATE_ATTRIBUTE(attr);

        Entry entry = GET_OR_CREATE_ENTRY(opcode, attr);

        if (!entry.TryAcquire())
        {
            System.Threading.Interlocked.Increment(ref s_totalRejected);
            lease = default;
            return false;
        }

        try
        {
            if (entry.Sem.Wait(0))
            {
                entry.Touch();
                System.Threading.Interlocked.Increment(ref s_totalAcquired);

                lease = new Lease(entry.Sem, entry);
                return true;
            }

            System.Threading.Interlocked.Increment(ref s_totalRejected);
            lease = default;
            return false;
        }
        catch (System.ObjectDisposedException)
        {
            // Entry was disposed - treat as rejection
            System.Threading.Interlocked.Increment(ref s_totalRejected);
            lease = default;
            return false;
        }
        catch (System.Exception ex)
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public async System.Threading.Tasks.ValueTask<Lease> EnterAsync(
        System.UInt16 opcode,
        [System.Diagnostics.CodeAnalysis.NotNull] PacketConcurrencyLimitAttribute attr,
        System.Threading.CancellationToken ct = default)
    {
        VALIDATE_ATTRIBUTE(attr);

        // FIX #13: Create timeout CTS properly
        using System.Threading.CancellationTokenSource timeoutCts = new();
        timeoutCts.CancelAfter(s_timeout);

        using System.Threading.CancellationTokenSource linkedCts =
            System.Threading.CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        Entry entry = GET_OR_CREATE_ENTRY(opcode, attr);

        if (!entry.TryAcquire())
        {
            throw new ConcurrencyRejectedException(
                $"Entry for opcode {opcode:X4} is being disposed");
        }

        try
        {
            // No queue: immediate attempt only
            if (!entry.Queue)
            {
                if (!entry.Sem.Wait(0, linkedCts.Token))
                {
                    System.Threading.Interlocked.Increment(ref s_totalRejected);
                    throw new ConcurrencyRejectedException(
                        $"Concurrency limit reached for opcode {opcode:X4} (no queue)");
                }

                entry.Touch();
                System.Threading.Interlocked.Increment(ref s_totalAcquired);

                return new Lease(entry.Sem, entry);
            }

            // Queue enabled
            return await ENTER_WITH_QUEUE_ASYNC(entry, opcode, linkedCts.Token).ConfigureAwait(false);
        }
        catch (System.OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            System.Threading.Interlocked.Increment(ref s_totalRejected);
            throw new System.TimeoutException(
                $"Concurrency gate timeout after {s_timeout.TotalSeconds}s for opcode {opcode:X4}");
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
        System.Int64 TotalAcquired,
        System.Int64 TotalRejected,
        System.Int64 TotalQueued,
        System.Int64 TotalCleaned,
        System.Int64 CircuitBreakerTrips,
        System.Boolean CircuitBreakerOpen,
        System.Int32 TrackedOpcodes
    ) GetStatistics()
    {
        return (
            System.Threading.Interlocked.Read(ref s_totalAcquired),
            System.Threading.Interlocked.Read(ref s_totalRejected),
            System.Threading.Interlocked.Read(ref s_totalQueued),
            System.Threading.Interlocked.Read(ref s_totalCleanedEntries),
            System.Threading.Interlocked.Read(ref s_circuitBreakerTrips),
            System.Threading.Volatile.Read(ref s_circuitBreakerOpen) == 1,
            s_table.Count
        );
    }

    /// <summary>
    /// Resets statistics. For testing only.
    /// </summary>
    internal void ResetStatistics()
    {
        System.Threading.Interlocked.Exchange(ref s_totalAcquired, 0);
        System.Threading.Interlocked.Exchange(ref s_totalRejected, 0);
        System.Threading.Interlocked.Exchange(ref s_totalQueued, 0);
        System.Threading.Interlocked.Exchange(ref s_totalCleanedEntries, 0);
        System.Threading.Interlocked.Exchange(ref s_circuitBreakerTrips, 0);
        System.Threading.Interlocked.Exchange(ref s_circuitBreakerOpen, 0);
    }

    #endregion Public API

    #region Report Generation

    /// <summary>
    /// Generates a human-readable diagnostic report of the concurrency gate state.
    /// </summary>
    [System.Diagnostics.StackTraceHidden]
    public System.String GenerateReport()
    {
        // Take snapshot
        System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<System.UInt16, Entry>> snapshot =
            [.. s_table];

        // Sort by load (highest pressure first)
        snapshot.Sort((a, b) =>
        {
            System.Int32 aPressure = a.Value.Capacity - a.Value.Sem.CurrentCount;
            System.Int32 bPressure = b.Value.Capacity - b.Value.Sem.CurrentCount;

            System.Int32 cmp = bPressure.CompareTo(aPressure);
            return cmp != 0 ? cmp : b.Value.QueueCount.CompareTo(a.Value.QueueCount);
        });

        // Calculate metrics
        var stats = GetStatistics();
        System.Double rejectionRate = 0.0;
        System.Int64 totalAttempts = stats.TotalAcquired + stats.TotalRejected;
        if (totalAttempts > 0)
        {
            rejectionRate = stats.TotalRejected * 100.0 / totalAttempts;
        }

        // Build report
        System.Text.StringBuilder sb = new();

        APPEND_REPORT_HEADER(sb, stats, rejectionRate);
        APPEND_OPCODE_DETAILS(sb, snapshot);

        return sb.ToString();
    }

    private void APPEND_REPORT_HEADER(
        System.Text.StringBuilder sb,
        (System.Int64 TotalAcquired, System.Int64 TotalRejected, System.Int64 TotalQueued,
         System.Int64 TotalCleaned, System.Int64 CircuitBreakerTrips, System.Boolean CircuitBreakerOpen,
         System.Int32 TrackedOpcodes) stats,
        System.Double rejectionRate)
    {
        sb.AppendLine($"[{System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] ConcurrencyGate Status:");
        sb.AppendLine($"CleanupInterval    : {CleanupInterval.TotalMinutes:F1} min");
        sb.AppendLine($"MinIdleAge         : {MinIdleAge.TotalMinutes:F1} min");
        sb.AppendLine($"TrackedOpcodes     : {stats.TrackedOpcodes}");
        sb.AppendLine($"TotalAcquired      : {stats.TotalAcquired:N0}");
        sb.AppendLine($"TotalRejected      : {stats.TotalRejected:N0}");
        sb.AppendLine($"TotalQueued        : {stats.TotalQueued:N0}");
        sb.AppendLine($"TotalCleaned       : {stats.TotalCleaned:N0}");
        sb.AppendLine($"RejectionRate      : {rejectionRate:F2}%");
        sb.AppendLine($"CircuitBreaker     : {(stats.CircuitBreakerOpen ? "OPEN" : "Closed")} (trips={stats.CircuitBreakerTrips})");
        sb.AppendLine();
    }

    private static void APPEND_OPCODE_DETAILS(
        System.Text.StringBuilder sb,
        System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<System.UInt16, Entry>> snapshot)
    {
        sb.AppendLine("Top Opcodes by Load:");
        sb.AppendLine("---------------------------------------------------------------------------------");
        sb.AppendLine("Opcode | Capacity | InUse | Avail | Queue | QueueMax | Queuing | LastUsed");
        sb.AppendLine("---------------------------------------------------------------------------------");

        if (snapshot.Count == 0)
        {
            sb.AppendLine("(no tracked opcodes)");
        }
        else
        {
            APPEND_TOP_OPCODES(sb, snapshot, maxRows: 50);
        }

        sb.AppendLine("---------------------------------------------------------------------------------");
    }

    private static void APPEND_TOP_OPCODES(
        System.Text.StringBuilder sb,
        System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<System.UInt16, Entry>> snapshot,
        System.Int32 maxRows)
    {
        System.Int32 rows = 0;

        foreach (var kvp in snapshot)
        {
            if (rows++ >= maxRows)
            {
                break;
            }

            System.UInt16 opcode = kvp.Key;
            Entry entry = kvp.Value;

            System.Int32 available = entry.Sem.CurrentCount;
            System.Int32 inUse = entry.Capacity - available;
            System.Int32 queueCount = entry.QueueCount;
            System.String queueEnabled = entry.Queue ? "yes" : " no";
            System.String queueMaxStr = entry.QueueMax == System.Int32.MaxValue ? "∞" : entry.QueueMax.ToString();
            System.DateTimeOffset lastUsed = entry.LastUsedUtc;

            sb.AppendLine(
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
    private System.Boolean IS_CIRCUIT_OPEN()
    {
        // Check if already open
        if (System.Threading.Volatile.Read(ref s_circuitBreakerOpen) == 1)
        {
            // Try to close if reset time passed
            System.Int64 resetTimeTicks = System.Threading.Volatile.Read(ref s_circuitBreakerResetTimeTicks);
            System.Int64 nowTicks = System.DateTime.UtcNow.Ticks;

            if (nowTicks >= resetTimeTicks)
            {
                if (System.Threading.Interlocked.CompareExchange(ref s_circuitBreakerOpen, 0, 1) == 1)
                {
                    // Reset counters
                    System.Threading.Interlocked.Exchange(ref s_totalAcquired, 0);
                    System.Threading.Interlocked.Exchange(ref s_totalRejected, 0);

                    s_logger?.Info($"[NW.{nameof(ConcurrencyGate)}] circuit breaker closed");
                }
            }

            return System.Threading.Volatile.Read(ref s_circuitBreakerOpen) == 1;
        }

        // Check if should open
        System.Int64 totalAttempts = System.Threading.Volatile.Read(ref s_totalAcquired) +
                                     System.Threading.Volatile.Read(ref s_totalRejected);

        if (totalAttempts < CircuitBreakerMinSamples)
        {
            return false;
        }

        System.Double rejectionRate = (System.Double)System.Threading.Volatile.Read(ref s_totalRejected) / totalAttempts;

        if (rejectionRate > CircuitBreakerThreshold)
        {
            if (System.Threading.Interlocked.CompareExchange(ref s_circuitBreakerOpen, 1, 0) == 0)
            {
                System.Int64 resetTime = System.DateTime.UtcNow.AddSeconds(CircuitBreakerResetAfterSeconds).Ticks;
                System.Threading.Interlocked.Exchange(ref s_circuitBreakerResetTimeTicks, resetTime);

                s_logger?.Error(
                    $"[NW.{nameof(ConcurrencyGate)}] circuit breaker opened " +
                    $"(rejection_rate={rejectionRate:P2}, attempts={totalAttempts})");
            }

            return true;
        }

        return false;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void VALIDATE_ATTRIBUTE(PacketConcurrencyLimitAttribute attr)
    {
        System.ArgumentNullException.ThrowIfNull(attr);

        if (attr.Max <= 0)
        {
            throw new System.ArgumentException(
                $"Concurrency max must be > 0, got {attr.Max}",
                nameof(attr));
        }

        if (attr.QueueMax < 0)
        {
            throw new System.ArgumentException(
                $"Queue max cannot be negative, got {attr.QueueMax}",
                nameof(attr));
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private Entry GET_OR_CREATE_ENTRY(
        System.UInt16 opcode,
        PacketConcurrencyLimitAttribute attr)
    {
        return s_table.GetOrAdd(
            opcode,
            _ => new Entry(attr.Max, attr.Queue, attr.QueueMax));
    }

    private async System.Threading.Tasks.ValueTask<Lease> ENTER_WITH_QUEUE_ASYNC(
        Entry entry,
        System.UInt16 opcode,
        System.Threading.CancellationToken ct)
    {
        if (!entry.TryIncrementQueue())
        {
            System.Threading.Interlocked.Increment(ref s_totalRejected);
            throw new ConcurrencyRejectedException(
                $"Concurrency queue is full for opcode {opcode:X4} " +
                $"(limit={entry.QueueMax}, current={entry.QueueCount})");
        }

        System.Threading.Interlocked.Increment(ref s_totalQueued);

        try
        {
            await entry.Sem.WaitAsync(ct).ConfigureAwait(false);

            entry.Touch();
            System.Threading.Interlocked.Increment(ref s_totalAcquired);

            return new Lease(entry.Sem, entry);
        }
        catch (System.OperationCanceledException)
        {
            System.Threading.Interlocked.Increment(ref s_totalRejected);
            throw;
        }
        finally
        {
            entry.DecrementQueue();
        }
    }

    [System.Diagnostics.StackTraceHidden]
    private void CLEANUP_IDLE_ENTRIES()
    {
        try
        {
            System.DateTimeOffset now = System.DateTimeOffset.UtcNow;
            System.Int32 removed = 0;

            foreach (var kvp in s_table)
            {
                System.UInt16 opcode = kvp.Key;
                Entry entry = kvp.Value;

                if (!entry.IsIdle)
                {
                    continue;
                }

                System.TimeSpan age = now - entry.LastUsedUtc;
                if (age < MinIdleAge)
                {
                    continue;
                }

                // Remove before disposal to prevent new usage
                if (s_table.TryRemove(opcode, out Entry removedEntry))
                {
                    removedEntry.Dispose();
                    removed++;
                    System.Threading.Interlocked.Increment(ref s_totalCleanedEntries);
                }
            }

            if (removed > 0)
            {
                s_logger?.Debug($"[NW.{nameof(ConcurrencyGate)}] cleanup removed={removed} remaining={s_table.Count}");
            }
        }
        catch (System.Exception ex)
        {
            s_logger?.Error($"[NW.{nameof(ConcurrencyGate)}] cleanup-error", ex);
        }
    }

    #endregion Private Methods
}
