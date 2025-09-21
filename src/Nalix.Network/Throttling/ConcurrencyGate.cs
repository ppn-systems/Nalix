// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Exceptions;
using Nalix.Common.Packets.Attributes;

namespace Nalix.Network.Throttling;

/// <summary>
/// Lightweight per-opcode concurrency guard with optional FIFO queuing.
/// Uses SemaphoreSlim per opcode. Queue length can be capped.
/// </summary>
public static class ConcurrencyGate
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<System.UInt16, Entry> s_table = new();

    private sealed class Entry(System.Int32 max, System.Boolean queue, System.Int32 queueMax)
    {
        public readonly System.Boolean Queue = queue;
        public readonly System.Int32 QueueMax = queueMax < 0 ? 0 : queueMax;
        public readonly System.Threading.SemaphoreSlim Sem = new(System.Math.Max(1, max));

        public System.Int32 QueueCount;               // current queued waiters (best-effort)
    }

    /// <summary>
    /// Represents a lease on a concurrency slot acquired from <see cref="ConcurrencyGate"/>.
    /// Disposing this struct releases the slot back to the semaphore.
    /// </summary>
    public readonly struct Lease(System.Threading.SemaphoreSlim sem) : System.IDisposable
    {
        private readonly System.Threading.SemaphoreSlim? _sem = sem;

        /// <inheritdoc/>
        public void Dispose() => _sem?.Release();
    }

    /// <summary>
    /// Try to enter immediately (no waiting). Returns false when full.
    /// </summary>
    public static System.Boolean TryEnter(System.UInt16 opcode, PacketConcurrencyLimitAttribute attr, out Lease lease)
    {
        Entry e = s_table.GetOrAdd(opcode, _ => new Entry(attr.Max, attr.Queue, attr.QueueMax));

        if (e.Sem.Wait(0))
        {
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
    public static async System.Threading.Tasks.ValueTask<Lease> EnterAsync(
        System.UInt16 opcode,
        PacketConcurrencyLimitAttribute attr,
        System.Threading.CancellationToken ct)
    {
        Entry e = s_table.GetOrAdd(opcode, _ => new Entry(attr.Max, attr.Queue, attr.QueueMax));

        if (!e.Queue)
        {
            if (!e.Sem.Wait(0, ct))
            {
                throw new ConcurrencyRejectedException("Concurrency limit reached (no queue).");
            }
            return new Lease(e.Sem);
        }

        // Queue enabled: cap queue length if configured
        if (e.QueueMax > 0)
        {
            System.Int32 q = System.Threading.Interlocked.Increment(ref e.QueueCount);
            if (q > e.QueueMax)
            {
                System.Threading.Interlocked.Decrement(ref e.QueueCount);
                throw new ConcurrencyRejectedException("Concurrency queue is full.");
            }

            try
            {
                await e.Sem.WaitAsync(ct).ConfigureAwait(false);
                return new Lease(e.Sem);
            }
            finally
            {
                System.Threading.Interlocked.Decrement(ref e.QueueCount);
            }
        }
        else
        {
            await e.Sem.WaitAsync(ct).ConfigureAwait(false);
            return new Lease(e.Sem);
        }
    }
}
