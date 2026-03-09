// Copyright (c) 2025 PPN Corporation. All rights reserved.
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
public static class ReliableClientSubscriptions
{
    // Cached logger — avoids repeated DI lookups on the hot receive path.
    private static ILogger Logger
        => InstanceManager.Instance.GetExistingInstance<ILogger>();

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
                if (!InstanceManager.Instance.GetExistingInstance<IPacketCatalog>()
                        .TryDeserialize(buffer.Span, out IPacket p) || p is not TPacket t)
                {
                    return;
                }

                handler(t);
            }
            catch (System.Exception ex)
            {
                // Swallow handler exceptions — must not fault FRAME_READER receive loop.
                Logger?.Error($"[SDK.On<{typeof(TPacket).Name}>] handler-error: {ex.Message}", ex);
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
                if (!InstanceManager.Instance.GetExistingInstance<IPacketCatalog>()
                        .TryDeserialize(buffer.Span, out IPacket p))
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
                Logger?.Error($"[SDK.On(predicate)] handler-error: {ex.Message}", ex);
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
                Logger?.Error(
                    $"[SDK.OnOnce<{typeof(TPacket).Name}>] handler-error: {ex.Message}", ex);
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