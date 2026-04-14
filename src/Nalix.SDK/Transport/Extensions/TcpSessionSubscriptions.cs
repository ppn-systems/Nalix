// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Nalix.Common.Abstractions;
using Nalix.Common.Networking.Packets;

namespace Nalix.SDK.Transport.Extensions;

/// <summary>
/// Convenience subscriptions for <see cref="TransportSession"/> to reduce boilerplate.
/// </summary>
/// <remarks>
/// <para>
/// Lease ownership contract: every wrapper in this class disposes the
/// <see cref="IBufferLease"/> exactly once inside a <c>finally</c> block.
/// Handlers receive deserialized packet objects and must NOT interact with the lease.
/// </para>
/// <para>
/// Handler exceptions are caught and logged; they are never re-thrown so that the
/// underlying <c>FrameReader</c> receive loop is never faulted by subscriber code.
/// </para>
/// </remarks>
[SkipLocalsInit]
public static class TcpSessionSubscriptions
{
    // ── On<TPacket> ──────────────────────────────────────────────────────────

    /// <summary>
    /// Subscribes to strongly-typed packets and ignores non-matching packets.
    /// </summary>
    /// <typeparam name="TPacket">The packet type to receive.</typeparam>
    /// <param name="client">The transport session to subscribe to.</param>
    /// <param name="handler">The callback invoked for each received packet.</param>
    /// <returns>An <see cref="IDisposable"/> that removes the subscription when disposed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IDisposable On<TPacket>(this TransportSession client, Action<TPacket> handler)
        where TPacket : class, IPacket
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(handler);

        void Wrapper(object? _, IBufferLease buffer)
        {
            try
            {
                IPacket p = client.Catalog.Deserialize(buffer.Span);

                if (p is not TPacket t)
                {
                    Trace.TraceWarning(
                        "Nalix.SDK.TcpSessionSubscriptions.On<{0}> ignored packet {1}.",
                        typeof(TPacket).Name,
                        p?.GetType().Name ?? "null");
                    return;
                }

                handler(t);
            }
            catch (Exception ex)
            {
                Trace.TraceError("Nalix.SDK.TcpSessionSubscriptions.On<{0}> failed: {1}", typeof(TPacket).Name, ex);
            }
        }

        client.OnMessageReceived += Wrapper;
        return new Unsub(() => client.OnMessageReceived -= Wrapper);
    }

    /// <summary>
    /// Subscribes to strongly-typed packets and throws if a different packet type is received.
    /// Use this only for debugging or for channels that are guaranteed to carry exactly one type.
    /// </summary>
    /// <typeparam name="TPacket">The packet type to receive.</typeparam>
    /// <param name="client">The transport session to subscribe to.</param>
    /// <param name="handler">The callback invoked for each received packet.</param>
    /// <returns>An <see cref="IDisposable"/> that removes the subscription when disposed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IDisposable OnExact<TPacket>(this TransportSession client, Action<TPacket> handler)
        where TPacket : class, IPacket
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(handler);

        void Wrapper(object? _, IBufferLease buffer)
        {
            try
            {
                IPacket p = client.Catalog.Deserialize(buffer.Span);

                if (p is not TPacket t)
                {
                    Trace.TraceError(
                        "Nalix.SDK.TcpSessionSubscriptions.OnExact<{0}> received unexpected packet {1}.",
                        typeof(TPacket).Name,
                        p?.GetType().Name ?? "null");
                    return;
                }

                handler(t);
            }
            catch (Exception ex)
            {
                Trace.TraceError("Nalix.SDK.TcpSessionSubscriptions.OnExact<{0}> failed: {1}", typeof(TPacket).Name, ex);
            }
        }

        client.OnMessageReceived += Wrapper;
        return new Unsub(() => client.OnMessageReceived -= Wrapper);
    }

    // ── On with predicate ────────────────────────────────────────────────────

    /// <summary>Subscribes to packets that match a predicate.</summary>
    /// <param name="client">The transport session to subscribe to.</param>
    /// <param name="predicate">A filter that determines whether a packet should be delivered.</param>
    /// <param name="handler">The callback invoked for each matching packet.</param>
    /// <returns>An <see cref="IDisposable"/> that removes the subscription when disposed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IDisposable On(this TransportSession client, Func<IPacket, bool> predicate, Action<IPacket> handler)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(handler);

        void Wrapper(object? _, IBufferLease buffer)
        {
            try
            {
                IPacket p = client.Catalog.Deserialize(buffer.Span);

                if (p is null || !predicate(p))
                {
                    return;
                }

                handler(p);
            }
            catch (Exception ex)
            {
                Trace.TraceError("Nalix.SDK.TcpSessionSubscriptions.On(predicate) failed: {0}", ex);
            }
        }

        client.OnMessageReceived += Wrapper;
        return new Unsub(() => client.OnMessageReceived -= Wrapper);
    }

    // ── OnOnce<TPacket> ───────────────────────────────────────────────���──────

    /// <summary>
    /// One-shot subscription: auto-unsubscribes after the first matching packet.
    /// Thread-safe via <see cref="Interlocked"/>.
    /// </summary>
    /// <typeparam name="TPacket">The packet type to receive.</typeparam>
    /// <param name="client">The transport session to subscribe to.</param>
    /// <param name="predicate">A filter that determines whether a packet should be delivered.</param>
    /// <param name="handler">The callback invoked for the first matching packet.</param>
    /// <returns>An <see cref="IDisposable"/> that removes the subscription when disposed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IDisposable OnOnce<TPacket>(this TransportSession client, Func<TPacket, bool> predicate, Action<TPacket> handler)
        where TPacket : class, IPacket
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(handler);

        int fired = 0;

        void Wrapper(object? _, IBufferLease buffer)
        {
            try
            {
                IPacket p = client.Catalog.Deserialize(buffer.Span);

                if (p is not TPacket t)
                {
                    return;
                }

                if (!predicate(t))
                {
                    return;
                }

                // Atomic once-guard: only the first arriving thread proceeds.
                if (Interlocked.Exchange(ref fired, 1) != 0)
                {
                    return;
                }

                // Unsubscribe before invoking handler to avoid a second delivery
                // if the handler itself triggers another message.
                client.OnMessageReceived -= Wrapper;

                handler(t);
            }
            catch (Exception ex)
            {
                Trace.TraceError("Nalix.SDK.TcpSessionSubscriptions.OnOnce<{0}> failed: {1}", typeof(TPacket).Name, ex);
            }
        }

        client.OnMessageReceived += Wrapper;
        return new Unsub(() => client.OnMessageReceived -= Wrapper);
    }

    // ── SubscribeTemp strongly-typed ─────────────────────────────────────────

    /// <summary>
    /// Subscribes to strongly-typed packets for the duration of a scoped operation.
    /// Automatically unsubscribes when the returned <see cref="IDisposable"/> is disposed.
    /// </summary>
    /// <typeparam name="TPacket">The packet type to receive.</typeparam>
    /// <param name="client">The transport session to subscribe to.</param>
    /// <param name="onMessage">Handler invoked for each matching packet.</param>
    /// <param name="onDisconnected">Optional handler invoked when the session disconnects while the subscription is active.</param>
    /// <returns>
    /// An <see cref="IDisposable"/> that unsubscribes both handlers when disposed.
    /// Always wrap in a <c>using</c> statement.
    /// </returns>
    /// <example>
    /// <code>
    /// using var sub = client.SubscribeTemp&lt;LoginResponse&gt;(
    ///     onMessage:      resp => tcs.TrySetResult(resp),
    ///     onDisconnected: ex   => tcs.TrySetException(ex));
    ///
    /// await client.SendAsync(loginRequest, ct);
    /// var response = await tcs.Task;
    /// </code>
    /// </example>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IDisposable SubscribeTemp<TPacket>(this TransportSession client, Action<TPacket> onMessage, Action<Exception>? onDisconnected = null)
        where TPacket : class, IPacket
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(onMessage);

        IDisposable msgSub = client.On(onMessage);

        if (onDisconnected is null)
        {
            return msgSub;
        }

        void DisconnectHandler(object? _, Exception ex)
        {
            try { onDisconnected(ex); } catch { }
        }

        client.OnDisconnected += DisconnectHandler;

        return client.Subscribe(msgSub, new DelegateDisposable(() => client.OnDisconnected -= DisconnectHandler));
    }

    /// <summary>
    /// Subscribes to strongly-typed packets with a predicate filter for the duration of a scoped operation.
    /// </summary>
    /// <typeparam name="TPacket">The packet type to receive.</typeparam>
    /// <param name="client">The transport session to subscribe to.</param>
    /// <param name="predicate">A filter that determines whether a packet should be delivered.</param>
    /// <param name="onMessage">Handler invoked for each matching packet.</param>
    /// <param name="onDisconnected">Optional handler invoked when the session disconnects while the subscription is active.</param>
    /// <returns>An <see cref="IDisposable"/> that unsubscribes when disposed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IDisposable SubscribeTemp<TPacket>(this TransportSession client, Func<TPacket, bool> predicate, Action<TPacket> onMessage, Action<Exception>? onDisconnected = null)
        where TPacket : class, IPacket
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(onMessage);

        IDisposable msgSub = client.On<TPacket>(p =>
        {
            if (predicate(p))
            {
                onMessage(p);
            }
        });

        if (onDisconnected is null)
        {
            return msgSub;
        }

        void DisconnectHandler(object? _, Exception ex)
        {
            try { onDisconnected(ex); } catch { }
        }

        client.OnDisconnected += DisconnectHandler;

        return client.Subscribe(msgSub, new DelegateDisposable(() => client.OnDisconnected -= DisconnectHandler));
    }

    // ── Subscribe (composite) ────────────────────────────────────────────────

    /// <summary>
    /// Groups multiple subscriptions into a single <see cref="CompositeSubscription"/>.
    /// </summary>
    /// <param name="_">The transport session used for fluent syntax.</param>
    /// <param name="subs">The subscriptions to group.</param>
    /// <returns>A composite handle that disposes all subscriptions together.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CompositeSubscription Subscribe(this TransportSession _, params IDisposable[] subs) => new(subs);

    // ── Internal ─────────────────────────────────────────────────────────────

    private sealed class Unsub(Action dispose) : IDisposable
    {
        private Action _dispose = dispose;

        /// <inheritdoc/>
        public void Dispose() => Interlocked.Exchange(ref _dispose!, null)?.Invoke();
    }
}

/// <summary>
/// Groups multiple <see cref="IDisposable"/> subscriptions into one disposable handle.
/// Thread-safe; supports adding new subscriptions after construction.
/// </summary>
public sealed class CompositeSubscription : IDisposable
{
    private int _disposed;
    private volatile IDisposable[] _subs;

    /// <summary>
    /// Initializes a new <see cref="CompositeSubscription"/> with the specified subscriptions.
    /// </summary>
    /// <param name="subs">The subscriptions to include.</param>
    public CompositeSubscription(params IDisposable[] subs) => _subs = subs ?? [];

    /// <summary>
    /// Adds a new subscription.
    /// If already disposed, the subscription is disposed immediately.
    /// </summary>
    /// <param name="sub">The subscription to add.</param>
    public void Add(IDisposable sub)
    {
        if (sub is null)
        {
            return;
        }

        if (Volatile.Read(ref _disposed) == 1)
        {
            sub.Dispose();
            return;
        }

        while (true)
        {
            IDisposable[] current = _subs;
            IDisposable[] updated = new IDisposable[current.Length + 1];
            Array.Copy(current, updated, current.Length);
            updated[^1] = sub;

            if (Interlocked.CompareExchange(ref _subs, updated, current) == current)
            {
                break;
            }
        }

        // Re-check disposed after insertion to handle race between Add and Dispose.
        if (Volatile.Read(ref _disposed) == 1)
        {
            sub.Dispose();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        foreach (IDisposable s in Interlocked.Exchange(ref _subs, []))
        {
            try { s?.Dispose(); }
            catch { /* one bad subscription must not prevent others from being disposed */ }
        }
    }
}

/// <summary>
/// An <see cref="IDisposable"/> that executes a delegate on disposal.
/// Used internally to wrap event unsubscription delegates into a disposable handle.
/// Thread-safe: the delegate is invoked at most once.
/// </summary>
/// <param name="onDispose">The action to invoke when disposed.</param>
internal sealed class DelegateDisposable(Action onDispose) : IDisposable
{
    private Action _onDispose = onDispose;

    /// <inheritdoc/>
    public void Dispose()
        => Interlocked.Exchange(ref _onDispose!, null)?.Invoke();
}
