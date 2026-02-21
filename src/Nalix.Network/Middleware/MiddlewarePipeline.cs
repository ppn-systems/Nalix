// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Attributes;
using Nalix.Common.Enums;
using Nalix.Common.Exceptions;
using Nalix.Network.Abstractions;
using Nalix.Network.Dispatch;

namespace Nalix.Network.Middleware;

/// <summary>
/// Represents a middleware pipeline for processing packets.
/// Allows chaining multiple middleware components to handle a packet context.
/// Middlewares are automatically sorted by their <see cref="MiddlewareOrderAttribute"/>.
/// </summary>
/// <typeparam name="TPacket">The type of packet being processed in the pipeline.</typeparam>
public class MiddlewarePipeline<TPacket>
{
    #region Fields

    private readonly System.Collections.Generic.List<MiddlewareEntry> _inbound = [];
    private readonly System.Collections.Generic.List<MiddlewareEntry> _outbound = [];
    private readonly System.Collections.Generic.List<MiddlewareEntry> _outboundAlways = [];

    private System.Boolean _isSorted;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets a value indicating whether the middleware pipeline contains no middleware components.
    /// </summary>
    public System.Boolean IsEmpty => _inbound.Count == 0 && _outbound.Count == 0 && _outboundAlways.Count == 0;

    #endregion Properties

    #region Public Methods

    /// <summary>
    /// Executes the pipeline asynchronously using the provided packet context and terminal handler.
    /// Middlewares are invoked in the order specified by their <see cref="MiddlewareOrderAttribute"/>.
    /// </summary>
    public System.Threading.Tasks.Task ExecuteAsync(
        PacketContext<TPacket> context,
        System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task> handler,
        System.Threading.CancellationToken ct = default)
    {
        ENSURE_SORTED();

        return INVOKE_PIPELINE_ASYNC(
            _inbound, context,
            async (downstreamCt) =>
            {
                await handler(downstreamCt).ConfigureAwait(false);

                await INVOKE_PIPELINE_ASYNC(
                    _outboundAlways, context,
                    (ct) =>
                    {
                        ct.ThrowIfCancellationRequested();
                        return System.Threading.Tasks.Task.CompletedTask;
                    },
                    downstreamCt
                ).ConfigureAwait(false);

                if (!context.SkipOutbound)
                {
                    await INVOKE_PIPELINE_ASYNC(
                        _outbound, context,
                        (ct) =>
                        {
                            ct.ThrowIfCancellationRequested();
                            return System.Threading.Tasks.Task.CompletedTask;
                        },
                        downstreamCt
                    ).ConfigureAwait(false);
                }
            },
            ct
        );
    }

    /// <summary>
    /// Adds a middleware component automatically to the appropriate stage based on its attributes.
    /// </summary>
    /// <param name="middleware">The middleware to add.</param>
    public void Use(IPacketMiddleware<TPacket> middleware)
    {
        System.Type type = middleware.GetType();
        System.Int32 order = GET_MIDDLEWARE_ORDER(type);
        MiddlewareStage stage = GET_MIDDLEWARE_STAGE(type);
        System.Boolean alwaysExecute = GET_ALWAYS_EXECUTE(type);

        MiddlewareEntry entry = new(middleware, order);

        switch (stage)
        {
            case MiddlewareStage.Inbound:
                _inbound.Add(entry);
                break;

            case MiddlewareStage.Outbound:
                if (alwaysExecute)
                {
                    _outboundAlways.Add(entry);
                }
                else
                {
                    _outbound.Add(entry);
                }
                break;

            case MiddlewareStage.Both:
                _inbound.Add(entry);
                if (alwaysExecute)
                {
                    _outboundAlways.Add(entry);
                }
                else
                {
                    _outbound.Add(entry);
                }
                break;
        }

        _isSorted = false;
    }

    /// <summary>
    /// Adds a middleware component to be executed before the main handler.
    /// </summary>
    public void UseInbound(IPacketMiddleware<TPacket> middleware)
    {
        System.Int32 order = GET_MIDDLEWARE_ORDER(middleware.GetType());
        _inbound.Add(new MiddlewareEntry(middleware, order));
        _isSorted = false;
    }

    /// <summary>
    /// Adds a middleware component to be executed after the main handler.
    /// </summary>
    public void UseOutbound(IPacketMiddleware<TPacket> middleware)
    {
        System.Int32 order = GET_MIDDLEWARE_ORDER(middleware.GetType());
        _outbound.Add(new MiddlewareEntry(middleware, order));
        _isSorted = false;
    }

    /// <summary>
    /// Adds a middleware component to be executed after the main handler, regardless of outbound skipping.
    /// </summary>
    public void UseOutboundAlways(IPacketMiddleware<TPacket> middleware)
    {
        System.Int32 order = GET_MIDDLEWARE_ORDER(middleware.GetType());
        _outboundAlways.Add(new MiddlewareEntry(middleware, order));
        _isSorted = false;
    }

    #endregion Public Methods

    #region Private Methods

    private void ENSURE_SORTED()
    {
        if (_isSorted)
        {
            return;
        }

        // Sort by order ascending (lower values execute first)
        _inbound.Sort((a, b) => a.Order.CompareTo(b.Order));

        // For outbound, reverse order (higher values execute first in outbound)
        _outbound.Sort((a, b) => b.Order.CompareTo(a.Order));
        _outboundAlways.Sort((a, b) => b.Order.CompareTo(a.Order));

        _isSorted = true;
    }

    private static System.Int32 GET_MIDDLEWARE_ORDER(System.Type middlewareType)
    {
        System.ArgumentNullException.ThrowIfNull(middlewareType);

        System.Object[] attributes = middlewareType.GetCustomAttributes(typeof(MiddlewareOrderAttribute), true);

        if (attributes.Length == 0)
        {
            return 0; // Default order
        }

        return attributes[0] is not MiddlewareOrderAttribute attr
            ? throw new InternalErrorException(
                $"Attribute retrieval failed for type '{middlewareType.FullName}'.",
                $"Expected '{nameof(MiddlewareOrderAttribute)}' but got '{attributes[0]?.GetType().FullName ?? "null"}'."
            )
            : attr.Order;
    }

    private static MiddlewareStage GET_MIDDLEWARE_STAGE(System.Type middlewareType)
    {
        System.ArgumentNullException.ThrowIfNull(middlewareType);

        System.Object[] attributes = middlewareType.GetCustomAttributes(typeof(MiddlewareStageAttribute), true);

        if (attributes.Length == 0)
        {
            return MiddlewareStage.Inbound; // Default stage
        }

        return attributes[0] is not MiddlewareStageAttribute attr
            ? throw new InternalErrorException(
                $"Attribute retrieval failed for type '{middlewareType.FullName}'.",
                $"Expected '{nameof(MiddlewareStageAttribute)}' but got '{attributes[0]?.GetType().FullName ?? "null"}'."
            )
            : attr.Stage;
    }

    private static System.Boolean GET_ALWAYS_EXECUTE(System.Type middlewareType)
    {
        System.ArgumentNullException.ThrowIfNull(middlewareType);

        System.Object[] attributes = middlewareType.GetCustomAttributes(typeof(MiddlewareStageAttribute), true);

        if (attributes.Length == 0)
        {
            return false; // Default: not always execute
        }

        return attributes[0] is not MiddlewareStageAttribute attr
            ? throw new InternalErrorException(
                $"Attribute retrieval failed for type '{middlewareType.FullName}'.",
                $"Expected '{nameof(MiddlewareStageAttribute)}' but got '{attributes[0]?.GetType().FullName ?? "null"}'."
            )
            : attr.AlwaysExecute;
    }

    private static System.Threading.Tasks.Task INVOKE_PIPELINE_ASYNC(
        System.Collections.Generic.List<MiddlewareEntry> middlewares, PacketContext<TPacket> context,
        System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task> final, System.Threading.CancellationToken startToken)
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1163:Unused parameter", Justification = "Token may be used by middleware")]
        static System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task> CreateWrapper(
            PacketContext<TPacket> context,
            IPacketMiddleware<TPacket> middleware,
            System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task> next)
        {
            return token =>
                middleware.InvokeAsync(
                    context,
                    downstreamToken => next(downstreamToken)
                );
        }

        System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task> next = final;

        for (System.Int32 i = middlewares.Count - 1; i >= 0; i--)
        {
            IPacketMiddleware<TPacket> current = middlewares[i].Middleware;
            System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task> localNext = next;
            next = CreateWrapper(context, current, localNext);
        }

        return next(startToken);
    }

    #endregion Private Methods

    #region Nested Types

    private readonly record struct MiddlewareEntry(IPacketMiddleware<TPacket> Middleware, System.Int32 Order);

    #endregion Nested Types
}