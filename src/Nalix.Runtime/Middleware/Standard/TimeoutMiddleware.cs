// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions;
using Nalix.Abstractions.Middleware;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Codec.Pooling;
using Nalix.Codec.ProtocolFrames;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Objects;
using Nalix.Runtime.Internal.RateLimiting;

namespace Nalix.Runtime.Middleware.Standard;

/// <summary>
/// Middleware that enforces a timeout for packet processing. If the next middleware or handler does not complete within the specified timeout,
/// a timeout response is sent to the client.
/// </summary>
[MiddlewareOrder(75)] // Execute late in inbound, wrap around handler
[MiddlewareStage(MiddlewareStage.Inbound)]
public sealed class TimeoutMiddleware : IPacketMiddleware<IPacket>
{
    private static readonly ObjectPoolManager s_pool = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>();

    static TimeoutMiddleware()
    {
        _ = s_pool.SetMaxCapacity<PooledCancellationTokenSource>(2048);
        _ = s_pool.Prealloc<PooledCancellationTokenSource>(16);
    }

    /// <inheritdoc/>
    public ValueTask InvokeAsync(IPacketContext<IPacket> context, Func<CancellationToken, ValueTask> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(context);

        int timeout = context.Attributes.Timeout?.TimeoutMilliseconds ?? 0;
        if (timeout <= 0)
        {
            // Hot path: no timeout configured — forward directly without async state machine.
            ValueTask pending = next(context.CancellationToken);
            if (pending.IsCompletedSuccessfully)
            {
#pragma warning disable CA1849 // Completed-success fast path.
                pending.GetAwaiter().GetResult();
#pragma warning restore CA1849
                return default;
            }
            return AWAIT_NEXT_ASYNC(pending);
        }

        // Cold path: timeout configured — requires CTS setup and async wrapping.
        return INVOKE_WITH_TIMEOUT_ASYNC(timeout, context, next);

        static async ValueTask AWAIT_NEXT_ASYNC(ValueTask operation) => await operation.ConfigureAwait(false);

        static async ValueTask INVOKE_WITH_TIMEOUT_ASYNC(int timeoutMs, IPacketContext<IPacket> ctx, Func<CancellationToken, ValueTask> nxt)
        {
            PooledCancellationTokenSource timeoutCts = s_pool.Get<PooledCancellationTokenSource>();
            timeoutCts.CancelAfter(timeoutMs);

            CancellationTokenRegistration reg = default;
            if (ctx.CancellationToken.CanBeCanceled)
            {
                reg = ctx.CancellationToken.UnsafeRegister(
                    static s => ((PooledCancellationTokenSource)s!).Cancel(), timeoutCts);
            }

            try
            {
                await ExecuteHandlerAsync(timeoutMs, ctx, nxt, timeoutCts.Token).ConfigureAwait(false);
            }
            finally
            {
#pragma warning disable CA1849
                reg.Dispose();
#pragma warning restore CA1849
                s_pool.Return<PooledCancellationTokenSource>(timeoutCts);
            }
        }
    }

    private static async ValueTask ExecuteHandlerAsync(
        int timeout,
        IPacketContext<IPacket> context,
        Func<CancellationToken, ValueTask> next,
        CancellationToken token)
    {
        try
        {
            await next(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested && !context.CancellationToken.IsCancellationRequested)
        {
            if (DirectiveGuard.TryAcquire(context.Connection,
                state => state.InboundDirectiveTimeoutLastSentAtMs,
                (state, val) => state.InboundDirectiveTimeoutLastSentAtMs = val))
            {
                return;
            }

            using PacketScope<Directive> lease = PacketFactory<Directive>.Acquire();
            Directive directive = lease.Value;

            directive.Initialize(
                ControlType.TIMEOUT, ProtocolReason.TIMEOUT, ProtocolAdvice.RETRY,
                sequenceId: context.Packet.Header.SequenceId,
                controlFlags: ControlFlags.IS_TRANSIENT,
                arg0: (uint)(timeout / 100));

            await context.Sender.SendAsync(directive, CancellationToken.None).ConfigureAwait(false);
        }
    }

    internal sealed class PooledCancellationTokenSource : IPoolable, IPoolRentable, IDisposable
    {
        private CancellationTokenSource _cts = new();

        public CancellationToken Token => _cts.Token;

        public bool IsActive { get; private set; }

        public void CancelAfter(int milliseconds) => _cts.CancelAfter(milliseconds);

        public void OnRent() => this.IsActive = true;

        public void ResetForPool()
        {
            this.IsActive = false;

            if (!_cts.TryReset())
            {
                _cts.Dispose();
                _cts = new CancellationTokenSource();
            }
        }
        public void Cancel() => _cts.Cancel();
        public void Dispose() => _cts.Dispose();
    }
}
