// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Middleware;
using Nalix.Common.Networking.Packets;
using Nalix.Network.Routing;

namespace Nalix.Network.Middleware;

/// <summary>
/// Represents a thread-safe middleware pipeline responsible for processing
/// packets through inbound and outbound stages with configurable error handling.
/// </summary>
/// <typeparam name="TPacket">
/// The packet type being processed by the middleware pipeline.
/// </typeparam>
/// <remarks>
/// <para>
/// The pipeline supports three execution stages:
/// <list type="bullet">
/// <item><description><b>Inbound</b>: Executed before the main packet handler.</description></item>
/// <item><description><b>Outbound</b>: Executed after the handler, in reverse order.</description></item>
/// <item><description><b>OutboundAlways</b>: Executed after the handler regardless of cancellation or errors.</description></item>
/// </list>
/// </para>
/// <para>
/// Middleware ordering is controlled via attributes and cached for performance.
/// The pipeline creates immutable execution snapshots to avoid locking during execution.
/// </para>
/// </remarks>
internal class MiddlewarePipeline<TPacket> where TPacket : IPacket
{
    #region Fields

    private readonly System.Threading.Lock _lock = new();
    private readonly System.Collections.Generic.List<MiddlewareEntry> _inbound = [];
    private readonly System.Collections.Generic.List<MiddlewareEntry> _outbound = [];
    private readonly System.Collections.Generic.List<MiddlewareEntry> _outboundAlways = [];
    private readonly System.Collections.Generic.HashSet<IPacketMiddleware<TPacket>> _registeredMiddlewares = [];

    private volatile System.Boolean _isSorted;
    private System.Boolean _continueOnError;
    private System.Action<System.Exception, System.Type> _errorHandler;

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<System.Type, MiddlewareMetadata>
        s_metadataCache = new();

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets a value indicating whether the pipeline contains no registered middleware.
    /// </summary>
    /// <value>
    /// <see langword="true"/> if no inbound or outbound middleware is registered;
    /// otherwise, <see langword="false"/>.
    /// </value>
    public System.Boolean IsEmpty
    {
        get
        {
            lock (_lock)
            {
                return _inbound.Count == 0 && _outbound.Count == 0 && _outboundAlways.Count == 0;
            }
        }
    }

    #endregion Properties

    #region Public Methods

    /// <summary>
    /// Configures how the pipeline handles exceptions thrown by middleware.
    /// </summary>
    /// <param name="continueOnError">
    /// A value indicating whether execution should continue
    /// after a middleware throws an exception.
    /// </param>
    /// <param name="errorHandler">
    /// An optional callback invoked when an exception occurs,
    /// providing the exception and the middleware type.
    /// </param>
    public void ConfigureErrorHandling(
        System.Boolean continueOnError,
        System.Action<System.Exception, System.Type> errorHandler = null)
    {
        lock (_lock)
        {
            _continueOnError = continueOnError;
            _errorHandler = errorHandler;
        }
    }

    /// <summary>
    /// Executes the middleware pipeline for the specified packet context.
    /// </summary>
    /// <param name="context">
    /// The packet execution context shared across middleware.
    /// </param>
    /// <param name="handler">
    /// The final handler invoked after inbound middleware execution.
    /// </param>
    /// <param name="ct">
    /// A cancellation token used to cancel pipeline execution.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous execution of the pipeline.
    /// </returns>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="context"/> or <paramref name="handler"/> is <see langword="null"/>.
    /// </exception>
    public System.Threading.Tasks.Task ExecuteAsync(
        PacketContext<TPacket> context,
        System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task> handler,
        System.Threading.CancellationToken ct = default)
    {
        System.ArgumentNullException.ThrowIfNull(context);
        System.ArgumentNullException.ThrowIfNull(handler);

        // Create immutable snapshots
        System.Collections.Generic.List<MiddlewareEntry> inboundSnapshot;
        System.Collections.Generic.List<MiddlewareEntry> outboundSnapshot;
        System.Collections.Generic.List<MiddlewareEntry> outboundAlwaysSnapshot;
        System.Boolean continueOnError;
        System.Action<System.Exception, System.Type> errorHandler;

        lock (_lock)
        {
            ENSURE_SORTED_UNSAFE();
            inboundSnapshot = [.. _inbound];
            outboundSnapshot = [.. _outbound];
            outboundAlwaysSnapshot = [.. _outboundAlways];
            continueOnError = _continueOnError;
            errorHandler = _errorHandler;
        }

        return INVOKE_PIPELINE_ASYNC(
            inboundSnapshot, context,
            async (inboundCt) =>
            {
                using var handlerCts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(inboundCt, ct);

                try
                {
                    await handler(handlerCts.Token).ConfigureAwait(false);
                }
                catch (System.OperationCanceledException)
                {
                    // Continue to outbound-always even if cancelled
                }

                await INVOKE_PIPELINE_ASYNC(
                    outboundAlwaysSnapshot, context,
                    (ct) =>
                    {
                        ct.ThrowIfCancellationRequested();
                        return System.Threading.Tasks.Task.CompletedTask;
                    },
                    ct,
                    continueOnError,
                    errorHandler
                ).ConfigureAwait(false);

                if (!context.SkipOutbound && !handlerCts.Token.IsCancellationRequested)
                {
                    await INVOKE_PIPELINE_ASYNC(
                        outboundSnapshot, context,
                        (ct) =>
                        {
                            ct.ThrowIfCancellationRequested();
                            return System.Threading.Tasks.Task.CompletedTask;
                        },
                        inboundCt,
                        continueOnError,
                        errorHandler
                    ).ConfigureAwait(false);
                }
            },
            ct,
            continueOnError,
            errorHandler
        );
    }

    /// <summary>
    /// Registers a middleware instance into the pipeline.
    /// </summary>
    /// <param name="middleware">
    /// The middleware instance to register.
    /// </param>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="middleware"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when the middleware instance has already been registered.
    /// </exception>
    public void Use(IPacketMiddleware<TPacket> middleware)
    {
        System.ArgumentNullException.ThrowIfNull(middleware);

        lock (_lock)
        {
            if (!_registeredMiddlewares.Add(middleware))
            {
                throw new System.InvalidOperationException(
                    $"Middleware '{middleware.GetType().FullName}' already registered");
            }

            MiddlewareMetadata metadata = GET_MIDDLEWARE_METADATA(middleware.GetType());
            MiddlewareEntry entry = new(middleware, metadata.Order);

            switch (metadata.Stage)
            {
                case MiddlewareStage.Inbound:
                    _inbound.Add(entry);
                    break;
                case MiddlewareStage.Outbound:
                    (metadata.AlwaysExecute ? _outboundAlways : _outbound).Add(entry);
                    break;
                case MiddlewareStage.Both:
                    _inbound.Add(entry);
                    (metadata.AlwaysExecute ? _outboundAlways : _outbound).Add(entry);
                    break;
                default:
                    throw new System.ArgumentOutOfRangeException();
            }

            _isSorted = false;
        }
    }

    /// <summary>
    /// Removes all registered middleware from the pipeline.
    /// </summary>
    /// <remarks>
    /// After calling this method, the pipeline will be empty
    /// and require middleware to be registered again.
    /// </remarks>
    public void Clear()
    {
        lock (_lock)
        {
            _inbound.Clear();
            _outbound.Clear();
            _outboundAlways.Clear();
            _registeredMiddlewares.Clear();
            _isSorted = false;
        }
    }

    #endregion Public Methods

    #region Private Methods

    private void ENSURE_SORTED_UNSAFE()
    {
        if (_isSorted)
        {
            return;
        }

        _inbound.Sort((a, b) => a.Order.CompareTo(b.Order));
        _outbound.Sort((a, b) => b.Order.CompareTo(a.Order));
        _outboundAlways.Sort((a, b) => b.Order.CompareTo(a.Order));

        _isSorted = true;
    }

    private static MiddlewareMetadata GET_MIDDLEWARE_METADATA(System.Type middlewareType)
    {
        System.ArgumentNullException.ThrowIfNull(middlewareType);

        return s_metadataCache.GetOrAdd(middlewareType, static type =>
        {
            System.Int32 order = 0;
            MiddlewareStage stage = MiddlewareStage.Inbound;
            System.Boolean alwaysExecute = false;

            if (System.Attribute.GetCustomAttribute(type, typeof(MiddlewareOrderAttribute))
                is MiddlewareOrderAttribute orderAttr)
            {
                order = orderAttr.Order;
            }

            if (System.Attribute.GetCustomAttribute(type, typeof(MiddlewareStageAttribute))
                is MiddlewareStageAttribute stageAttr)
            {
                stage = stageAttr.Stage;
                alwaysExecute = stageAttr.AlwaysExecute;
            }

            return new MiddlewareMetadata(order, stage, alwaysExecute);
        });
    }

    private static System.Threading.Tasks.Task INVOKE_PIPELINE_ASYNC(
        System.Collections.Generic.List<MiddlewareEntry> middlewares,
        PacketContext<TPacket> context,
        System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task> final,
        System.Threading.CancellationToken startToken,
        System.Boolean continueOnError = false,
        System.Action<System.Exception, System.Type> errorHandler = null)
    {
        static System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task> CreateWrapper(
            PacketContext<TPacket> context,
            IPacketMiddleware<TPacket> middleware,
            System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task> next,
            System.Boolean continueOnError,
            System.Action<System.Exception, System.Type> errorHandler)
        {
            return async token =>
            {
                try
                {
                    await middleware.InvokeAsync(context, next).ConfigureAwait(false);
                }
                catch (System.Exception ex) when (continueOnError)
                {
                    errorHandler?.Invoke(ex, middleware.GetType());
                    await next(token).ConfigureAwait(false);
                }
            };
        }

        System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task> next = final;

        for (System.Int32 i = middlewares.Count - 1; i >= 0; i--)
        {
            IPacketMiddleware<TPacket> current = middlewares[i].Middleware;
            System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task> localNext = next;
            next = CreateWrapper(context, current, localNext, continueOnError, errorHandler);
        }

        return next(startToken);
    }

    #endregion Private Methods

    #region Nested Types

    private readonly record struct MiddlewareEntry(IPacketMiddleware<TPacket> Middleware, System.Int32 Order);

    private readonly record struct MiddlewareMetadata(
        System.Int32 Order,
        MiddlewareStage Stage,
        System.Boolean AlwaysExecute);

    #endregion Nested Types
}