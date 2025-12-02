// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Exceptions;
using Nalix.Common.Packets.Attributes;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Tasks;

namespace Nalix.Network.Throttling;

/// <summary>
/// Lightweight per-opcode concurrency guard with optional FIFO queuing.
/// Uses SemaphoreSlim per opcode. Queue length can be capped.
/// </summary>
public static class ConcurrencyGate
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<System.UInt16, Entry> s_table = new();

    static ConcurrencyGate()
    {
        _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleRecurring(
            $"{nameof(ConcurrencyGate)}.cleanup", System.TimeSpan.FromMinutes(1),
            static _ =>
            {
                System.DateTimeOffset now = System.DateTimeOffset.UtcNow;
                System.TimeSpan minIdleAge = System.TimeSpan.FromMinutes(10);

                foreach (System.Collections.Generic.KeyValuePair<System.UInt16, Entry> kv in s_table)
                {
                    System.UInt16 opcode = kv.Key;
                    Entry entry = kv.Value;

                    if (!entry.IsIdle)
                    {
                        continue;
                    }

                    System.TimeSpan age = now - entry.LastUsedUtc;
                    if (age < minIdleAge)
                    {
                        continue;
                    }

                    if (s_table.TryRemove(opcode, out Entry removed))
                    {
                        try { removed.Sem.Dispose(); }
                        catch { /* ignored */ }
                    }
                }

                return System.Threading.Tasks.ValueTask.CompletedTask;
            },
            new RecurringOptions
            {
                NonReentrant = true,
                Tag = nameof(ConcurrencyGate)
            });
    }

    private sealed class Entry(System.Int32 max, System.Boolean queue, System.Int32 queueMax)
    {
        private System.Int64 _lastUsedUtcTicks;

        public readonly System.Boolean Queue = queue;
        public readonly System.Int32 Capacity = System.Math.Max(1, max);
        public readonly System.Int32 QueueMax = queueMax < 0 ? 0 : queueMax;
        public readonly System.Threading.SemaphoreSlim Sem = new(System.Math.Max(1, max));

        public System.Int32 QueueCount;               // current queued waiters (best-effort)

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Touch()
        {
            var nowTicks = System.DateTimeOffset.UtcNow.UtcDateTime.Ticks;
            _ = System.Threading.Interlocked.Exchange(ref _lastUsedUtcTicks, nowTicks);
        }

        public System.DateTimeOffset LastUsedUtc
        {
            get
            {
                var ticks = System.Threading.Interlocked.Read(ref _lastUsedUtcTicks);
                return new System.DateTimeOffset(ticks, System.TimeSpan.Zero);
            }
        }

        /// <summary>
        /// Entry is idle when no slots are in use and queue is empty (best-effort).
        /// </summary>
        public System.Boolean IsIdle
            => Sem.CurrentCount == Capacity && System.Threading.Volatile.Read(ref QueueCount) == 0;
    }

    /// <summary>
    /// Represents a lease on a concurrency slot acquired from <see cref="ConcurrencyGate"/>.
    /// Disposing this struct releases the slot back to the semaphore.
    /// </summary>
    public readonly struct Lease(System.Threading.SemaphoreSlim sem) : System.IDisposable
    {
        [System.Diagnostics.CodeAnalysis.AllowNull]
        private readonly System.Threading.SemaphoreSlim _sem = sem;

        /// <inheritdoc/>
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Dispose() => _sem?.Release();
    }

    /// <summary>
    /// Try to enter immediately (no waiting). Returns false when full.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean TryEnter(
        [System.Diagnostics.CodeAnalysis.NotNull] System.UInt16 opcode,
        [System.Diagnostics.CodeAnalysis.NotNull] PacketConcurrencyLimitAttribute attr,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Lease lease)
    {
        Entry e = s_table.GetOrAdd(opcode, _ => new Entry(attr.Max, attr.Queue, attr.QueueMax));

        if (e.Sem.Wait(0))
        {
            e.Touch();
            lease = new Lease(e.Sem);
            return true;
        }

        lease = default;
        return false;
    }

    /// <summary>
    /// Enter with optional waiting when Queue == true. Throws OperationCanceledException if cancelled.
    /// Enforces QueueMax if > 0.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static async System.Threading.Tasks.ValueTask<Lease> EnterAsync(
        [System.Diagnostics.CodeAnalysis.NotNull] System.UInt16 opcode,
        [System.Diagnostics.CodeAnalysis.NotNull] PacketConcurrencyLimitAttribute attr,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Threading.CancellationToken ct = default)
    {
        Entry e = s_table.GetOrAdd(opcode, _ => new Entry(attr.Max, attr.Queue, attr.QueueMax));

        // No queue: immediate attempt only
        if (!e.Queue)
        {
            if (!e.Sem.Wait(0, ct))
            {
                throw new ConcurrencyRejectedException("Concurrency limit reached (no queue).");
            }

            e.Touch();
            return new Lease(e.Sem);
        }

        // Queue enabled: cap queue length if configured
        if (e.QueueMax > 0)
        {
            System.Int32 q = System.Threading.Interlocked.Increment(ref e.QueueCount);
            if (q > e.QueueMax)
            {
                _ = System.Threading.Interlocked.Decrement(ref e.QueueCount);
                throw new ConcurrencyRejectedException("Concurrency queue is full.");
            }

            try
            {
                await e.Sem.WaitAsync(ct).ConfigureAwait(false);

                e.Touch();
                return new Lease(e.Sem);
            }
            finally
            {
                _ = System.Threading.Interlocked.Decrement(ref e.QueueCount);
            }
        }
        else
        {
            await e.Sem.WaitAsync(ct).ConfigureAwait(false);

            e.Touch();
            return new Lease(e.Sem);
        }
    }
}
