// Copyright (c) 2025 PPN Corporation. All rights reserved. 

using Nalix.Common.Core.Exceptions;
using Nalix.Common.Diagnostics;
using Nalix.Common.Messaging.Packets.Attributes;
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
public static class ConcurrencyGate
{
    #region Constants

    /// <summary>
    /// Minimum idle time before entry is eligible for cleanup.
    /// </summary>
    private static readonly System.TimeSpan MinIdleAge = System.TimeSpan.FromMinutes(10);

    /// <summary>
    /// Cleanup interval for removing stale entries.
    /// </summary>
    private static readonly System.TimeSpan CleanupInterval = System.TimeSpan.FromMinutes(1);

    #endregion Constants

    #region Fields

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<System.UInt16, Entry> s_table = new();

    [System.Diagnostics.CodeAnalysis.AllowNull]
    private static readonly ILogger s_logger = InstanceManager.Instance.GetExistingInstance<ILogger>();

    private static System.Int64 s_totalAcquired;
    private static System.Int64 s_totalRejected;
    private static System.Int64 s_totalQueued;
    private static System.Int64 s_totalCleanedEntries;

    #endregion Fields

    #region Static Constructor

    /// <summary>
    /// Initializes the cleanup task.
    /// </summary>
    static ConcurrencyGate()
    {
        try
        {
            _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleRecurring(
                name: $"{nameof(ConcurrencyGate)}.cleanup",
                interval: CleanupInterval,
                work: static _ =>
                {
                    CleanupIdleEntries();
                    return System.Threading.Tasks.ValueTask.CompletedTask;
                },
                options: new RecurringOptions
                {
                    NonReentrant = true,
                    Tag = nameof(ConcurrencyGate),
                    Jitter = System.TimeSpan.FromSeconds(10),
                    ExecutionTimeout = System.TimeSpan.FromSeconds(5)
                });

            s_logger?.Debug($"[NW.{nameof(ConcurrencyGate)}] initialized with cleanup interval={CleanupInterval.TotalMinutes: F1}min");
        }
        catch (System.Exception ex)
        {
            s_logger?.Error($"[NW.{nameof(ConcurrencyGate)}] initialization-error msg={ex.Message}");
        }
    }

    #endregion Static Constructor

    #region Entry Class

    /// <summary>
    /// Represents a per-opcode concurrency limiter with reference counting for safe disposal.
    /// </summary>
    public sealed class Entry : System.IDisposable
    {
        private System.Int32 _queueCount;
        private System.Int32 _activeUsers; // Reference count
        private System.Int64 _lastUsedUtcTicks;
        private System.Int32 _disposed;

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

        /// <inheritdoc/>
        public Entry(System.Int32 max, System.Boolean queue, System.Int32 queueMax)
        {
            Queue = queue;
            Capacity = System.Math.Max(1, max);
            QueueMax = queueMax < 0 ? 0 : queueMax;
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
            _ = System.Threading.Interlocked.Exchange(ref _lastUsedUtcTicks, nowTicks);
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
                if (_activeUsers > 0 || System.Threading.Volatile.Read(ref _disposed) != 0)
                {
                    return false;
                }

                // Check both semaphore and queue atomically (best effort)
                System.Int32 queue = System.Threading.Volatile.Read(ref _queueCount);
                System.Int32 available = Sem.CurrentCount;

                return available == Capacity && queue == 0;
            }
        }

        /// <summary>
        /// Attempts to acquire usage reference.  Returns false if disposed.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public System.Boolean TryAcquire()
        {
            if (System.Threading.Volatile.Read(ref _disposed) != 0)
            {
                return false;
            }

            _ = System.Threading.Interlocked.Increment(ref _activeUsers);

            // Double-check after increment
            if (System.Threading.Volatile.Read(ref _disposed) != 0)
            {
                _ = System.Threading.Interlocked.Decrement(ref _activeUsers);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Releases usage reference.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Release() => _ = System.Threading.Interlocked.Decrement(ref _activeUsers);

        /// <summary>
        /// Attempts to increment queue count if under limit.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public System.Boolean TryIncrementQueue()
        {
            if (QueueMax <= 0)
            {
                return true; // No limit
            }

            // ✅ FIX: Atomic check-and-increment
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

                // Retry on race
            }
        }

        /// <summary>
        /// Decrements queue count.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void DecrementQueue() => _ = System.Threading.Interlocked.Decrement(ref _queueCount);

        /// <summary>
        /// Safely disposes the semaphore after waiting for active users.
        /// </summary>
        public void Dispose()
        {
            // Atomic check-and-set: 0 -> 1
            if (System.Threading.Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            {
                return;
            }

            // Wait briefly for active users
            System.Int32 waited = 0;
            System.Int32 spinCount = 0;
            const System.Int32 maxWaitMs = 100;

            while (System.Threading.Interlocked.CompareExchange(ref _activeUsers, 0, 0) > 0
                   && waited < maxWaitMs)
            {
                if (spinCount++ < 10)
                {
                    System.Threading.Thread.SpinWait(100);
                }
                else
                {
                    System.Threading.Thread.Sleep(1);
                    waited++;
                }
            }

            // Dispose semaphore
            try
            {
                Sem.Dispose();
            }
            catch (System.Exception ex)
            {
                s_logger?.Error($"[NW.{nameof(ConcurrencyGate)}:Entry] disposal-error msg={ex.Message}");
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
        private readonly System.Threading.SemaphoreSlim _sem = sem ?? throw new System.ArgumentNullException(nameof(sem));
        private readonly Entry _entry = entry ?? throw new System.ArgumentNullException(nameof(entry));

        /// <summary>
        /// Releases the concurrency slot.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
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
                s_logger?.Error($"[NW.{nameof(ConcurrencyGate)}:Lease] release-error msg={ex.Message}");
            }
            finally
            {
                _entry?.Release();
            }
        }
    }

    #endregion Lease Struct

    #region Public API

    /// <summary>
    /// Attempts to enter immediately without waiting.
    /// </summary>
    /// <param name="opcode">Operation code to limit. </param>
    /// <param name="attr">Concurrency limit configuration.</param>
    /// <param name="lease">Output lease if successful.</param>
    /// <returns>True if slot acquired; false if full.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.Boolean TryEnter(
        System.UInt16 opcode,
        [System.Diagnostics.CodeAnalysis.NotNull] PacketConcurrencyLimitAttribute attr, out Lease lease)
    {
        ValidateAttribute(attr);

        Entry entry = GetOrCreateEntry(opcode, attr);

        if (!entry.TryAcquire())
        {
            lease = default;
            return false;
        }

        try
        {
            if (entry.Sem.Wait(0))
            {
                entry.Touch();
                _ = System.Threading.Interlocked.Increment(ref s_totalAcquired);

                lease = new Lease(entry.Sem, entry);
                return true;
            }

            _ = System.Threading.Interlocked.Increment(ref s_totalRejected);
            entry.Release();

            lease = default;
            return false;
        }
        catch
        {
            entry.Release();
            throw;
        }
    }

    /// <summary>
    /// Enters with optional waiting when queuing is enabled.
    /// </summary>
    /// <param name="opcode">Operation code to limit.</param>
    /// <param name="attr">Concurrency limit configuration.</param>
    /// <param name="ct">Cancellation token. </param>
    /// <returns>Lease that must be disposed to release the slot.</returns>
    /// <exception cref="ConcurrencyRejectedException">Thrown when limit is reached.</exception>
    /// <exception cref="System.OperationCanceledException">Thrown when cancelled.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static async System.Threading.Tasks.ValueTask<Lease> EnterAsync(
        System.UInt16 opcode,
        [System.Diagnostics.CodeAnalysis.NotNull] PacketConcurrencyLimitAttribute attr,
        System.Threading.CancellationToken ct = default)
    {
        ValidateAttribute(attr);

        Entry entry = GetOrCreateEntry(opcode, attr);

        if (!entry.TryAcquire())
        {
            throw new ConcurrencyRejectedException(
                $"Entry for opcode {opcode: X4} is being disposed");
        }

        try
        {
            // No queue:  immediate attempt only
            if (!entry.Queue)
            {
                if (!entry.Sem.Wait(0, ct))
                {
                    _ = System.Threading.Interlocked.Increment(ref s_totalRejected);
                    throw new ConcurrencyRejectedException(
                        $"Concurrency limit reached for opcode {opcode:X4} (no queue)");
                }

                entry.Touch();
                _ = System.Threading.Interlocked.Increment(ref s_totalAcquired);

                return new Lease(entry.Sem, entry);
            }

            // Queue enabled
            return await EnterWithQueueAsync(entry, opcode, ct).ConfigureAwait(false);
        }
        catch
        {
            entry.Release();
            throw;
        }
    }

    #endregion Public API

    #region Report Generation

    /// <summary>
    /// Generates a human-readable diagnostic report of the concurrency gate state.
    /// Includes configuration, global metrics, and top opcodes by load.
    /// </summary>
    /// <returns>Formatted string report.</returns>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static System.String GenerateReport()
    {
        // Take snapshot
        var snapshot = new System.Collections.Generic.List<
            System.Collections.Generic.KeyValuePair<System.UInt16, Entry>>(s_table.Count);

        snapshot.AddRange(s_table);

        // Sort by load (lowest available slots = highest load)
        snapshot.Sort((a, b) =>
        {
            // Calculate "pressure" = capacity - available
            System.Int32 aPressure = a.Value.Capacity - a.Value.Sem.CurrentCount;
            System.Int32 bPressure = b.Value.Capacity - b.Value.Sem.CurrentCount;

            System.Int32 cmp = bPressure.CompareTo(aPressure);
            if (cmp != 0)
            {
                return cmp;
            }

            // Tie-break by queue count
            return b.Value.QueueCount.CompareTo(a.Value.QueueCount);
        });

        // Calculate global metrics
        System.Int64 totalAcquired = System.Threading.Interlocked.Read(ref s_totalAcquired);
        System.Int64 totalRejected = System.Threading.Interlocked.Read(ref s_totalRejected);
        System.Int64 totalQueued = System.Threading.Interlocked.Read(ref s_totalQueued);
        System.Int64 totalCleaned = System.Threading.Interlocked.Read(ref s_totalCleanedEntries);

        System.Double rejectionRate = 0.0;
        System.Int64 totalAttempts = totalAcquired + totalRejected;
        if (totalAttempts > 0)
        {
            rejectionRate = totalRejected * 100.0 / totalAttempts;
        }

        // Build report
        System.Text.StringBuilder sb = new();

        AppendReportHeader(sb, snapshot.Count, totalAcquired, totalRejected,
                          totalQueued, totalCleaned, rejectionRate);
        AppendOpcodeDetails(sb, snapshot);

        return sb.ToString();
    }

    /// <summary>
    /// Appends report header with configuration and global metrics.
    /// </summary>
    private static void AppendReportHeader(
        System.Text.StringBuilder sb,
        System.Int32 trackedOpcodes,
        System.Int64 totalAcquired,
        System.Int64 totalRejected,
        System.Int64 totalQueued,
        System.Int64 totalCleaned,
        System.Double rejectionRate)
    {
        _ = sb.AppendLine($"[{System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] ConcurrencyGate Status:");
        _ = sb.AppendLine($"CleanupInterval    : {CleanupInterval.TotalMinutes:F1} min");
        _ = sb.AppendLine($"MinIdleAge         : {MinIdleAge.TotalMinutes:F1} min");
        _ = sb.AppendLine($"TrackedOpcodes     : {trackedOpcodes}");
        _ = sb.AppendLine($"TotalAcquired      : {totalAcquired: N0}");
        _ = sb.AppendLine($"TotalRejected      : {totalRejected:N0}");
        _ = sb.AppendLine($"TotalQueued        : {totalQueued:N0}");
        _ = sb.AppendLine($"TotalCleaned       : {totalCleaned:N0}");
        _ = sb.AppendLine($"RejectionRate      : {rejectionRate:F2}%");
        _ = sb.AppendLine();
    }

    /// <summary>
    /// Appends detailed opcode information table.
    /// </summary>
    private static void AppendOpcodeDetails(
        System.Text.StringBuilder sb,
        System.Collections.Generic.List<
            System.Collections.Generic.KeyValuePair<System.UInt16, Entry>> snapshot)
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
            AppendTopOpcodes(sb, snapshot, maxRows: 50);
        }

        _ = sb.AppendLine("---------------------------------------------------------------------------------");
    }

    /// <summary>
    /// Appends top N opcodes to report.
    /// </summary>
    private static void AppendTopOpcodes(
        System.Text.StringBuilder sb,
        System.Collections.Generic.List<
            System.Collections.Generic.KeyValuePair<System.UInt16, Entry>> snapshot,
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
            System.String queueMaxStr = entry.QueueMax > 0 ? entry.QueueMax.ToString() : "∞";
            System.DateTimeOffset lastUsed = entry.LastUsedUtc;

            _ = sb.AppendLine(
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
    /// Validates concurrency limit attribute.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void ValidateAttribute(PacketConcurrencyLimitAttribute attr)
    {
        System.ArgumentNullException.ThrowIfNull(attr);

        if (attr.Max <= 0)
        {
            throw new System.ArgumentException(
                $"Concurrency max must be > 0, got {attr.Max}",
                nameof(attr));
        }
    }

    /// <summary>
    /// Gets or creates entry for opcode.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static Entry GetOrCreateEntry(
        System.UInt16 opcode,
        PacketConcurrencyLimitAttribute attr)
    {
        return s_table.GetOrAdd(
            opcode,
            _ => new Entry(attr.Max, attr.Queue, attr.QueueMax));
    }

    /// <summary>
    /// Enters with queue support and proper queue limit enforcement.
    /// </summary>
    private static async System.Threading.Tasks.ValueTask<Lease> EnterWithQueueAsync(
        Entry entry,
        System.UInt16 opcode,
        System.Threading.CancellationToken ct)
    {
        // ✅ FIX:  Atomic queue limit check
        if (!entry.TryIncrementQueue())
        {
            _ = System.Threading.Interlocked.Increment(ref s_totalRejected);
            throw new ConcurrencyRejectedException(
                $"Concurrency queue is full for opcode {opcode: X4} " +
                $"(limit={entry.QueueMax}, current={entry.QueueCount})");
        }

        _ = System.Threading.Interlocked.Increment(ref s_totalQueued);

        try
        {
            // ✅ FIX: Proper cancellation handling
            System.Boolean acquired = false;

            try
            {
                await entry.Sem.WaitAsync(ct).ConfigureAwait(false);
                acquired = true;
            }
            catch (System.OperationCanceledException)
            {
                // If cancelled after acquiring, release immediately
                if (acquired)
                {
                    entry.Sem.Release();
                }
                throw;
            }

            entry.Touch();
            _ = System.Threading.Interlocked.Increment(ref s_totalAcquired);

            return new Lease(entry.Sem, entry);
        }
        finally
        {
            entry.DecrementQueue();
        }
    }

    /// <summary>
    /// Cleans up idle entries.
    /// </summary>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void CleanupIdleEntries()
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

                // ✅ FIX: Remove before disposal to prevent new usage
                if (s_table.TryRemove(opcode, out Entry removedEntry))
                {
                    removedEntry.Dispose();
                    removed++;
                    _ = System.Threading.Interlocked.Increment(ref s_totalCleanedEntries);
                }
            }

            if (removed > 0)
            {
                s_logger?.Debug($"[NW.{nameof(ConcurrencyGate)}] cleanup removed={removed} remaining={s_table.Count}");
            }
        }
        catch (System.Exception ex)
        {
            s_logger?.Error($"[NW.{nameof(ConcurrencyGate)}] cleanup-error msg={ex.Message}");
        }
    }

    #endregion Private Methods
}