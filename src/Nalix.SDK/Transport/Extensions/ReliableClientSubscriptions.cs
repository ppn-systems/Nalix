// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Networking.Caching;
using Nalix.Common.Networking.Packets.Abstractions;
using Nalix.Common.Networking.Transport;
using Nalix.Framework.Injection;

namespace Nalix.SDK.Transport.Extensions;

/// <summary>
/// Convenience subscriptions for <see cref="IClientConnection"/> to reduce boilerplate.
/// </summary>
/// <remarks>
/// All wrappers correctly dispose the <see cref="IBufferLease"/> after deserialization
/// to prevent memory leaks from the pooled buffer.
/// </remarks>
[System.Runtime.CompilerServices.SkipLocalsInit]
public static class ReliableClientSubscriptions
{
    /// <summary>
    /// Subscribes to strongly-typed packets. Returns <see cref="System.IDisposable"/> for easy unsubscription.
    /// </summary>
    /// <typeparam name="TPacket">The packet type to filter for.</typeparam>
    /// <param name="client">The client connection to subscribe to.</param>
    /// <param name="handler">The action invoked for each matching packet.</param>
    /// <returns>An <see cref="System.IDisposable"/> that unsubscribes when disposed.</returns>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="client"/> or <paramref name="handler"/> is <c>null</c>.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.IDisposable On<TPacket>(
        this IClientConnection client,
        System.Action<TPacket> handler)
        where TPacket : class, IPacket
    {
        System.ArgumentNullException.ThrowIfNull(client);
        System.ArgumentNullException.ThrowIfNull(handler);

        void Wrapper(System.Object _, IBufferLease buffer)
        {
            using (buffer)
            {
                if (InstanceManager.Instance.GetExistingInstance<IPacketCatalog>()
                        .TryDeserialize(buffer.Span, out IPacket p) && p is TPacket t)
                {
                    handler(t);
                }
            }
        }

        client.OnMessageReceived += Wrapper;
        return new Unsub(() => client.OnMessageReceived -= Wrapper);
    }

    /// <summary>
    /// Subscribes with a predicate filter. Returns <see cref="System.IDisposable"/> for easy unsubscription.
    /// </summary>
    /// <param name="client">The client connection to subscribe to.</param>
    /// <param name="predicate">Filter predicate; only packets returning <c>true</c> are forwarded.</param>
    /// <param name="handler">The action invoked for each matching packet.</param>
    /// <returns>An <see cref="System.IDisposable"/> that unsubscribes when disposed.</returns>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="client"/>, <paramref name="predicate"/>, or <paramref name="handler"/> is <c>null</c>.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.IDisposable On(
        this IClientConnection client,
        System.Func<IPacket, System.Boolean> predicate,
        System.Action<IPacket> handler)
    {
        System.ArgumentNullException.ThrowIfNull(client);
        System.ArgumentNullException.ThrowIfNull(predicate);
        System.ArgumentNullException.ThrowIfNull(handler);

        void Wrapper(System.Object _, IBufferLease buffer)
        {
            using (buffer)
            {
                if (!InstanceManager.Instance.GetExistingInstance<IPacketCatalog>()
                        .TryDeserialize(buffer.Span, out IPacket p))
                {
                    return;
                }

                // Guard against null packet from a failed deserialization path.
                if (p is not null && predicate(p))
                {
                    handler(p);
                }
            }
        }

        client.OnMessageReceived += Wrapper;
        return new Unsub(() => client.OnMessageReceived -= Wrapper);
    }

    /// <summary>
    /// One-shot subscription: auto-unsubscribes after the first matching packet.
    /// Thread-safe via <see cref="System.Threading.Interlocked"/>.
    /// </summary>
    /// <typeparam name="TPacket">The packet type to filter for.</typeparam>
    /// <param name="client">The client connection to subscribe to.</param>
    /// <param name="predicate">Optional additional filter predicate.</param>
    /// <param name="handler">The action invoked for the first matching packet.</param>
    /// <returns>An <see cref="System.IDisposable"/> that cancels the subscription when disposed.</returns>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="client"/> or <paramref name="handler"/> is <c>null</c>.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.IDisposable OnOnce<TPacket>(
        this IClientConnection client,
        System.Func<TPacket, System.Boolean> predicate,
        System.Action<TPacket> handler)
        where TPacket : class, IPacket
    {
        System.ArgumentNullException.ThrowIfNull(client);
        System.ArgumentNullException.ThrowIfNull(handler);

        System.Int32 fired = 0;

        void Wrapper(System.Object _, IBufferLease buffer)
        {
            using (buffer)
            {
                if (!InstanceManager.Instance.GetExistingInstance<IPacketCatalog>()
                        .TryDeserialize(buffer.Span, out IPacket p))
                {
                    return;
                }

                if (p is not TPacket t)
                {
                    return;
                }

                if (predicate is not null && !predicate(t))
                {
                    return;
                }

                // Ensure exactly-once delivery even under concurrent invocations.
                if (System.Threading.Interlocked.Exchange(ref fired, 1) == 0)
                {
                    client.OnMessageReceived -= Wrapper; // unsubscribe first, then invoke
                    handler(t);
                }
            }
        }

        client.OnMessageReceived += Wrapper;
        return new Unsub(() => client.OnMessageReceived -= Wrapper);
    }

    /// <summary>
    /// Groups multiple subscriptions into a single <see cref="CompositeSubscription"/>.
    /// </summary>
    /// <param name="_">The client (unused; provided for fluent extension syntax).</param>
    /// <param name="subs">The subscriptions to group.</param>
    /// <returns>A <see cref="CompositeSubscription"/> that disposes all grouped subscriptions.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static CompositeSubscription Subscribe(
        this IClientConnection _,
        params System.IDisposable[] subs)
        => new(subs);

    // ── Internal helper ──────────────────────────────────────────────────────

    private sealed class Unsub(System.Action dispose) : System.IDisposable
    {
        private System.Action _dispose = dispose;

        /// <inheritdoc/>
        public void Dispose() => System.Threading.Interlocked.Exchange(ref _dispose, null)?.Invoke();
    }
}

/// <summary>
/// Groups multiple <see cref="System.IDisposable"/> subscriptions into one disposable handle.
/// Thread-safe; supports adding new subscriptions after construction.
/// </summary>
public sealed class CompositeSubscription : System.IDisposable
{
    private System.Int32 _disposed;
    private volatile System.IDisposable[] _subs;

    /// <summary>
    /// Initializes a new <see cref="CompositeSubscription"/> with the specified subscriptions.
    /// </summary>
    /// <param name="subs">Initial subscriptions to manage.</param>
    public CompositeSubscription(params System.IDisposable[] subs) => _subs = subs ?? [];

    /// <summary>
    /// Adds a new subscription.
    /// If already disposed, the subscription is disposed immediately.
    /// </summary>
    /// <param name="sub">The subscription to add.</param>
    public void Add(System.IDisposable sub)
    {
        if (sub is null)
        {
            return;
        }

        if (System.Threading.Volatile.Read(ref _disposed) == 1)
        {
            sub.Dispose();
            return;
        }

        // Spin-safe append using Interlocked.CompareExchange loop.
        while (true)
        {
            System.IDisposable[] current = _subs;
            System.IDisposable[] updated = new System.IDisposable[current.Length + 1];
            System.Array.Copy(current, updated, current.Length);
            updated[^1] = sub;

            if (System.Threading.Interlocked.CompareExchange(ref _subs, updated, current) == current)
            {
                break;
            }

            // Another thread mutated _subs concurrently; retry.
        }

        // Re-check disposed after insertion to handle a race between Add and Dispose.
        if (System.Threading.Volatile.Read(ref _disposed) == 1)
        {
            sub.Dispose();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (System.Threading.Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        System.IDisposable[] subs = System.Threading.Interlocked.Exchange(ref _subs, []);
        foreach (System.IDisposable s in subs)
        {
            try { s?.Dispose(); }
            catch { /* Swallow: one bad subscription must not prevent others from being disposed. */ }
        }
    }
}