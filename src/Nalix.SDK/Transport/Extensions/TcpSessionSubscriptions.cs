// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Diagnostics.Abstractions;
using Nalix.Common.Networking.Caching;
using Nalix.Common.Networking.Packets.Abstractions;
using Nalix.Common.Networking.Transport;
using Nalix.Framework.Injection;

namespace Nalix.SDK.Transport.Extensions;

/// <summary>
/// Convenience subscriptions for <see cref="IClientConnection"/> to reduce boilerplate.
/// </summary>
/// <remarks>
/// <para>
/// Lease ownership contract: every wrapper in this class disposes the
/// <see cref="IBufferLease"/> exactly once inside a <c>finally</c> block.
/// Handlers receive deserialized packet objects and must NOT interact with the lease.
/// </para>
/// <para>
/// Handler exceptions are caught and logged; they are never re-thrown so that the
/// underlying <c>FRAME_READER</c> receive loop is never faulted by subscriber code.
/// </para>
/// </remarks>
[System.Runtime.CompilerServices.SkipLocalsInit]
public static class TcpSessionSubscriptions
{
    private static readonly IPacketRegistry s_catalog = InstanceManager.Instance.GetExistingInstance<IPacketRegistry>();
    // ── On<TPacket> ──────────────────────────────────────────────────────────

    /// <summary>
    /// Subscribes to strongly-typed packets.
    /// Returns <see cref="System.IDisposable"/> for easy unsubscription.
    /// </summary>
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
            // Wrapper is the sole owner of the lease; always dispose in finally.
            try
            {
                if (!s_catalog.TryDeserialize(buffer.Span, out IPacket p) || p is not TPacket t)
                {
                    return;
                }

                handler(t);
            }
            catch (System.Exception ex)
            {
                // Swallow handler exceptions — must not fault FRAME_READER receive loop.
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Error($"[SDK.On<{typeof(TPacket).Name}>] handler-error: {ex.Message}", ex);
            }
            finally
            {
                // Guaranteed single disposal — this is the only Dispose call for the lease.
                try { buffer.Dispose(); } catch { }
            }
        }

        client.OnMessageReceived += Wrapper;
        return new Unsub(() => client.OnMessageReceived -= Wrapper);
    }

    // ── On with predicate ────────────────────────────────────────────────────

    /// <summary>
    /// Subscribes with a predicate filter.
    /// Returns <see cref="System.IDisposable"/> for easy unsubscription.
    /// </summary>
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
            try
            {
                if (!s_catalog.TryDeserialize(buffer.Span, out IPacket p))
                {
                    return;
                }

                if (p is null || !predicate(p))
                {
                    return;
                }

                handler(p);
            }
            catch (System.Exception ex)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Error($"[SDK.On(predicate)] handler-error: {ex.Message}", ex);
            }
            finally
            {
                try { buffer.Dispose(); } catch { }
            }
        }

        client.OnMessageReceived += Wrapper;
        return new Unsub(() => client.OnMessageReceived -= Wrapper);
    }

    // ── OnOnce<TPacket> ───────────────────────────────────────────────���──────

    /// <summary>
    /// One-shot subscription: auto-unsubscribes after the first matching packet.
    /// Thread-safe via <see cref="System.Threading.Interlocked"/>.
    /// </summary>
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
            // Wrapper is the sole owner of the lease — dispose in finally, always, exactly once.
            try
            {
                // Deserialize — if it fails, we still fall through to finally and dispose.
                if (!s_catalog.TryDeserialize(buffer.Span, out IPacket p))
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

                // Atomic once-guard: only the first arriving thread proceeds.
                if (System.Threading.Interlocked.Exchange(ref fired, 1) != 0)
                {
                    return;
                }

                // Unsubscribe before invoking handler to avoid a second delivery
                // if the handler itself triggers another message.
                client.OnMessageReceived -= Wrapper;

                handler(t);
            }
            catch (System.Exception ex)
            {
                // Swallow — must not bubble up to FRAME_READER receive loop.
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Error($"[SDK.OnOnce<{typeof(TPacket).Name}>] handler-error: {ex.Message}", ex);
            }
            finally
            {
                // Sole disposal point — runs whether handler succeeded, threw, or was skipped.
                try { buffer.Dispose(); } catch { }
            }
        }

        client.OnMessageReceived += Wrapper;
        return new Unsub(() => client.OnMessageReceived -= Wrapper);
    }

    // ── SubscribeTemp strongly-typed ─────────────────────────────────────────

    /// <summary>
    /// Subscribes to strongly-typed packets for the duration of a scoped operation.
    /// Automatically unsubscribes when the returned <see cref="System.IDisposable"/> is disposed.
    /// </summary>
    /// <typeparam name="TPacket">The expected packet type.</typeparam>
    /// <param name="client">The connected client.</param>
    /// <param name="onMessage">
    /// Handler invoked for each matching packet. The lease has already been disposed
    /// before this is called — do not interact with the raw buffer.
    /// </param>
    /// <param name="onDisconnected">
    /// Optional handler invoked when the client disconnects while the subscription is active.
    /// </param>
    /// <returns>
    /// An <see cref="System.IDisposable"/> that unsubscribes both handlers when disposed.
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.IDisposable SubscribeTemp<TPacket>(
        this IClientConnection client,
        System.Action<TPacket> onMessage,
        System.Action<System.Exception> onDisconnected = null)
        where TPacket : class, IPacket
    {
        System.ArgumentNullException.ThrowIfNull(client);
        System.ArgumentNullException.ThrowIfNull(onMessage);

        System.IDisposable msgSub = client.On<TPacket>(onMessage);

        if (onDisconnected is null)
        {
            return msgSub;
        }

        void DisconnectHandler(System.Object _, System.Exception ex)
        {
            try { onDisconnected(ex); } catch { }
        }

        client.OnDisconnected += DisconnectHandler;

        // Return a composite that unsubscribes both handlers atomically.
        return new CompositeSubscription(
            msgSub,
            new DelegateDisposable(() => client.OnDisconnected -= DisconnectHandler));
    }

    /// <summary>
    /// Subscribes to strongly-typed packets with a predicate filter for the duration of a scoped operation.
    /// </summary>
    /// <typeparam name="TPacket">The expected packet type.</typeparam>
    /// <param name="client">The connected client.</param>
    /// <param name="predicate">Filter — only packets for which this returns <c>true</c> are forwarded.</param>
    /// <param name="onMessage">Handler invoked for each matching packet.</param>
    /// <param name="onDisconnected">Optional disconnect handler.</param>
    /// <returns>An <see cref="System.IDisposable"/> that unsubscribes when disposed.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.IDisposable SubscribeTemp<TPacket>(
        this IClientConnection client,
        System.Func<TPacket, System.Boolean> predicate,
        System.Action<TPacket> onMessage,
        System.Action<System.Exception> onDisconnected = null)
        where TPacket : class, IPacket
    {
        System.ArgumentNullException.ThrowIfNull(client);
        System.ArgumentNullException.ThrowIfNull(predicate);
        System.ArgumentNullException.ThrowIfNull(onMessage);

        // Wrap predicate + handler into a single On<TPacket> subscription.
        System.IDisposable msgSub = client.On<TPacket>(p =>
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

        void DisconnectHandler(System.Object _, System.Exception ex)
        {
            try { onDisconnected(ex); } catch { }
        }

        client.OnDisconnected += DisconnectHandler;

        return new CompositeSubscription(
            msgSub,
            new DelegateDisposable(() => client.OnDisconnected -= DisconnectHandler));
    }

    // ── Subscribe (composite) ────────────────────────────────────────────────

    /// <summary>
    /// Groups multiple subscriptions into a single <see cref="CompositeSubscription"/>.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static CompositeSubscription Subscribe(
        this IClientConnection _,
        params System.IDisposable[] subs)
        => new(subs);

    // ── Internal ─────────────────────────────────────────────────────────────

    private sealed class Unsub(System.Action dispose) : System.IDisposable
    {
        private System.Action _dispose = dispose;

        /// <inheritdoc/>
        public void Dispose()
            => System.Threading.Interlocked.Exchange(ref _dispose, null)?.Invoke();
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
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "<Pending>")]
    public CompositeSubscription(params System.IDisposable[] subs) => _subs = subs ?? [];

    /// <summary>
    /// Adds a new subscription.
    /// If already disposed, the subscription is disposed immediately.
    /// </summary>
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
        }

        // Re-check disposed after insertion to handle race between Add and Dispose.
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
            catch { /* one bad subscription must not prevent others from being disposed */ }
        }
    }
}

/// <summary>
/// An <see cref="System.IDisposable"/> that executes a delegate on disposal.
/// Used internally to wrap event unsubscription delegates into a disposable handle.
/// Thread-safe: the delegate is invoked at most once.
/// </summary>
internal sealed class DelegateDisposable(System.Action onDispose) : System.IDisposable
{
    private System.Action _onDispose = onDispose;

    /// <inheritdoc/>
    public void Dispose()
        => System.Threading.Interlocked.Exchange(ref _onDispose, null)?.Invoke();
}