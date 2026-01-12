// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Infrastructure.Client;
using Nalix.Common.Messaging.Packets.Abstractions;

namespace Nalix.SDK.Remote.Extensions;

/// <summary>
/// Convenience subscriptions for ReliableClient to reduce boilerplate.
/// </summary>
[System.Runtime.CompilerServices.SkipLocalsInit]
public static class ReliableClientSubscriptions
{
    /// <summary>
    /// Subscribe to a typed packet. Returns IDisposable for quick unsubscribe.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.IDisposable On<TPacket>(
        this IReliableClient client,
        System.Action<TPacket> handler)
        where TPacket : class, IPacket
    {
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        void Wrapper(IPacket p)
        {
            if (p is TPacket t)
            {
                handler(t);
            }
        }
        client.PacketReceived += Wrapper;
        return new Unsub(() => client.PacketReceived -= Wrapper);
    }

    /// <summary>
    /// Subscribe with predicate filter. Returns IDisposable for quick unsubscribe.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.IDisposable On(
        this IReliableClient client,
        System.Func<IPacket, System.Boolean> predicate,
        System.Action<IPacket> handler)
    {

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        void Wrapper(IPacket p)
        {
            if (predicate(p))
            {
                handler(p);
            }
        }
        client.PacketReceived += Wrapper;
        return new Unsub(() => client.PacketReceived -= Wrapper);
    }

    /// <summary>
    /// One-shot subscribe: auto-unsubscribe after the first matching packet.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.IDisposable OnOnce<TPacket>(
        this IReliableClient client,
        System.Func<TPacket, System.Boolean> predicate,
        System.Action<TPacket> handler)
        where TPacket : class, IPacket
    {
        System.Int32 fired = 0;


        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        void Wrapper(IPacket p)
        {
            if (p is not TPacket t)
            {
                return;
            }

            if (predicate != null && !predicate(t))
            {
                return;
            }

            if (System.Threading.Interlocked.Exchange(ref fired, 1) == 0)
            {
                client.PacketReceived -= Wrapper; // remove first
                handler(t);                       // then invoke
            }
        }
        client.PacketReceived += Wrapper;
        return new Unsub(() => client.PacketReceived -= Wrapper);
    }


    /// <summary>
    /// Helper to group multiple subscriptions and dispose once.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static CompositeSubscription Subscribe(this ReliableClient _, params System.IDisposable[] subs) => new(subs);

    private sealed class Unsub(System.Action dispose) : System.IDisposable
    {
        private System.Action _dispose = dispose;

        public void Dispose() => System.Threading.Interlocked.Exchange(ref _dispose, null)?.Invoke();
    }
}

/// <summary>
/// Groups multiple IDisposable subscriptions into one disposable.
/// </summary>
public sealed class CompositeSubscription(params System.IDisposable[] subs) : System.IDisposable
{
    private System.IDisposable[] _subs = subs;
    private System.Int32 _disposed; // 0/1

    /// <summary>
    /// Adds a new subscription to the composite.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Add(System.IDisposable sub)
    {
        if (System.Threading.Interlocked.CompareExchange(ref _disposed, 0, 0) == 1)
        {
            sub.Dispose();
            return;
        }
        var old = _subs;
        var arr = new System.IDisposable[old.Length + 1];
        System.Array.Copy(old, arr, old.Length);
        arr[^1] = sub;
        _subs = arr;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (System.Threading.Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        var subs = System.Threading.Interlocked.Exchange(ref _subs, []);
        foreach (var s in subs) { try { s.Dispose(); } catch { /* swallow */ } }
    }
}
