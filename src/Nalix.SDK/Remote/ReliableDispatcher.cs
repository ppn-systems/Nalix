// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Infrastructure.Client;
using Nalix.Common.Messaging.Packets.Abstractions;
using System.Linq;

namespace Nalix.SDK.Remote;

/// <summary>
/// Thread-safe packet dispatcher.
/// - Supports Register&lt;T&gt;, RegisterOnce&lt;T&gt; with predicate.
/// - Dispatch(IPacket) calls matching handlers synchronously on the caller thread.
/// </summary>
public sealed class ReliableDispatcher : IReliableDispatcher
{
    // Use ConcurrentDictionary of Type -> immutable handler list for fast dispatch.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<System.Type, Handlers> _map = new();

    /// <summary>
    /// Gets a value indicating whether the dispatcher contains no registered handlers.
    /// </summary>
    public System.Boolean IsEmpty => _map.IsEmpty;

    private sealed class Handlers(System.Action<IPacket>[] persistent, OneShot[] oneShots)
    {
        public readonly System.Action<IPacket>[] Persistent = persistent;
        public readonly OneShot[] OneShots = oneShots;
    }

    private sealed class OneShot(System.Func<IPacket, System.Boolean> predicate, System.Action<IPacket> handler)
    {
        public readonly System.Func<IPacket, System.Boolean> Predicate = predicate;
        public readonly System.Action<IPacket> Handler = handler;
    }

    private System.Int32 _disposed;

    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1510:Use ArgumentNullException throw helper", Justification = "<Pending>")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1513:Use ObjectDisposedException throw helper", Justification = "<Pending>")]
    public void Register<TPacket>(System.Action<TPacket> handler) where TPacket : class, IPacket
    {
        if (handler is null)
        {
            throw new System.ArgumentNullException(nameof(handler));
        }

        if (System.Threading.Volatile.Read(ref _disposed) == 1)
        {
            throw new System.ObjectDisposedException(nameof(ReliableDispatcher));
        }

        System.Type t = typeof(TPacket);
        _ = _map.AddOrUpdate(t,
            _ => new Handlers([(p => handler((TPacket)p))], []),
            (_, old) =>
            {
                var list = new System.Action<IPacket>[old.Persistent.Length + 1];
                System.Array.Copy(old.Persistent, list, old.Persistent.Length);
                list[^1] = p => handler((TPacket)p);
                return new Handlers(list, old.OneShots);
            });
    }

    /// <inheritdoc/>
    public void RegisterOnce<TPacket>(System.Func<TPacket, System.Boolean> predicate, System.Action<TPacket> handler) where TPacket : class, IPacket
    {
        System.ArgumentNullException.ThrowIfNull(predicate);
        System.ArgumentNullException.ThrowIfNull(handler);
        System.ObjectDisposedException.ThrowIf(System.Threading.Volatile.Read(ref _disposed) == 1, nameof(ReliableDispatcher));

        System.Type t = typeof(TPacket);
        _ = _map.AddOrUpdate(t,
            _ => new Handlers([], [new OneShot(p => predicate((TPacket)p), p => handler((TPacket)p))]),
            (_, old) =>
            {
                var os = new OneShot[old.OneShots.Length + 1];
                System.Array.Copy(old.OneShots, os, old.OneShots.Length);
                os[^1] = new OneShot(p => predicate((TPacket)p), p => handler((TPacket)p));
                return new Handlers(old.Persistent, os);
            });
    }

    /// <inheritdoc/>
    public void Dispatch(IPacket packet)
    {
        if (packet is null)
        {
            return;
        }

        if (System.Threading.Volatile.Read(ref _disposed) == 1)
        {
            return;
        }

        var t = packet.GetType();
        if (!_map.TryGetValue(t, out var handlers))
        {
            // No handlers -- nothing to do
            return;
        }

        // Invoke persistent handlers
        foreach (System.Action<IPacket> h in handlers.Persistent)
        {
            try { h(packet); } catch { /* swallow - dispatcher shouldn't throw */ }
        }

        if (handlers.OneShots.Length == 0)
        {
            return;
        }

        // Evaluate one-shots and remove invoked ones atomically
        // We'll collect indices that matched and then replace handlers list without them.
        System.Collections.Generic.List<System.Int32> matchedIndexes = new(handlers.OneShots.Length);
        for (System.Int32 i = 0; i < handlers.OneShots.Length; i++)
        {
            try
            {
                if (handlers.OneShots[i].Predicate(packet))
                {
                    matchedIndexes.Add(i);
                    handlers.OneShots[i].Handler(packet);
                }
            }
            catch
            {
                // swallow predicate/handler exceptions
            }
        }

        if (matchedIndexes.Count == 0)
        {
            return;
        }

        // Remove matched one-shots from the stored array via CAS loop
        System.Boolean updated;
        do
        {
            updated = false;
            if (!_map.TryGetValue(t, out var current))
            {
                break;
            }

            // Build new one-shot array excluding the ones already invoked
            OneShot[] newOneShots = [.. current.OneShots.Where((os, idx) => !matchedIndexes.Contains(idx))];
            Handlers newHandlers = new(current.Persistent, newOneShots);
            updated = _map.TryUpdate(t, newHandlers, current);
        } while (!updated);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (System.Threading.Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        _map.Clear();
    }
}