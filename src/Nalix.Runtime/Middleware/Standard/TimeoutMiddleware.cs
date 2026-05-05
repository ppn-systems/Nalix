// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions;
using Nalix.Abstractions.Middleware;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Codec.DataFrames.SignalFrames;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Objects;
using Nalix.Runtime.Internal.RateLimiting;
using Nalix.Runtime.Pooling;

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

    /// <inheritdoc/>
    public async ValueTask InvokeAsync(IPacketContext<IPacket> context, Func<CancellationToken, ValueTask> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(context);

        int timeout = context.Attributes.Timeout?.TimeoutMilliseconds ?? 0;
        if (timeout <= 0)
        {
            await next(context.CancellationToken).ConfigureAwait(false);
            return;
        }

        PooledCancellationTokenSource timeoutCts = s_pool.Get<PooledCancellationTokenSource>();
        timeoutCts.CancelAfter(timeout);

        CancellationTokenRegistration reg = default;
        if (context.CancellationToken.CanBeCanceled)
        {
            reg = context.CancellationToken.UnsafeRegister(
                static s => ((PooledCancellationTokenSource)s!).Cancel(), timeoutCts);
        }

        try
        {
            await ExecuteHandlerAsync(timeout, context, next, timeoutCts.Token).ConfigureAwait(false);
        }
        finally
        {
#pragma warning disable CA1849 // Call async methods when in an async method
            reg.Dispose();
#pragma warning restore CA1849 // Call async methods when in an async method
            s_pool.Return<PooledCancellationTokenSource>(timeoutCts);
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
            if (!DirectiveGuard.TryAcquire(
                context.Connection,
                ConnectionAttributes.InboundDirectiveTimeoutLastSentAtMs))
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
