// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Middleware;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Objects;
using Nalix.Runtime.Dispatching;

namespace Nalix.Runtime.Middleware;

/// <summary>
/// Represents a thread-safe middleware pipeline responsible for processing packets
/// through inbound and outbound stages with configurable error handling.
/// </summary>
/// <typeparam name="TPacket">The packet type being processed by the pipeline.</typeparam>
internal sealed class MiddlewarePipeline<TPacket> where TPacket : IPacket
{
    #region Fields

    private readonly Lock _lock = new();
    private readonly List<MiddlewareEntry> _inbound = [];
    private readonly List<MiddlewareEntry> _outbound = [];
    private readonly List<MiddlewareEntry> _outboundAlways = [];
    private readonly HashSet<IPacketMiddleware<TPacket>> _registeredMiddlewares = [];

    private bool _isSorted;
    private bool _continueOnError;

    private Action<Exception, Type>? _errorHandler;
    private PipelineSnapshot _snapshot = PipelineSnapshot.Empty;

    private static readonly ConcurrentDictionary<Type, MiddlewareMetadata> s_metadataCache = new();
    private static readonly ObjectPoolManager s_pool = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>();

    // Instance-level local pool for pipeline contexts to bypass global pool management.
    // 32 slots is sufficient for high concurrency within a single packet pipeline.
    private readonly PooledPipelineContext[] _localPool = new PooledPipelineContext[32];
    private long _localPoolMask;

    #endregion Fields

    #region APIs

    /// <summary>
    /// Gets a value indicating whether the pipeline has no middleware in any stage.
    /// </summary>
    public bool IsEmpty => Volatile.Read(ref _snapshot).IsEmpty;

    /// <summary>
    /// Clears all registered middleware.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _inbound.Clear();
            _outbound.Clear();
            _outboundAlways.Clear();
            _registeredMiddlewares.Clear();
            _isSorted = true;
            Volatile.Write(ref _snapshot, PipelineSnapshot.Empty);
        }
    }

    /// <summary>
    /// Registers middleware into the pipeline.
    /// </summary>
    /// <param name="middleware">Middleware instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="middleware"/> is null.</exception>
    /// <exception cref="InternalErrorException">Thrown when middleware is already registered.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for unsupported stage metadata.</exception>
    public void Use(IPacketMiddleware<TPacket> middleware)
    {
        ArgumentNullException.ThrowIfNull(middleware);

        lock (_lock)
        {
            if (!_registeredMiddlewares.Add(middleware))
            {
                throw new InternalErrorException(
                    $"Middleware '{middleware.GetType().FullName}' already registered");
            }

            MiddlewareMetadata metadata = GetMiddlewareMetadata(middleware.GetType());
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
                    throw new ArgumentOutOfRangeException(nameof(middleware));
            }

            _isSorted = false;
            this.REBUILD_SNAPSHOT_UNSAFE();
        }
    }

    /// <summary>
    /// Configures error handling behavior for middleware execution.
    /// </summary>
    /// <param name="continueOnError">Whether to continue the chain after middleware exceptions.</param>
    /// <param name="errorHandler">Optional callback invoked for middleware exceptions.</param>
    public void ConfigureErrorHandling(bool continueOnError, Action<Exception, Type>? errorHandler = null)
    {
        lock (_lock)
        {
            _continueOnError = continueOnError;
            _errorHandler = errorHandler;
            this.REBUILD_SNAPSHOT_UNSAFE();
        }
    }

    /// <summary>
    /// Executes the middleware pipeline for a packet context.
    /// </summary>
    /// <param name="context">Packet execution context.</param>
    /// <param name="handler">Final packet handler.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A value task representing pipeline completion.</returns>
    public ValueTask ExecuteAsync(PacketContext<TPacket> context, Func<CancellationToken, ValueTask> handler, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(handler);

        PipelineSnapshot snapshot = Volatile.Read(ref _snapshot);
        if (snapshot.IsEmpty)
        {
            return handler(ct);
        }

        PooledPipelineContext? runner = this.AcquireRunner();
        runner ??= s_pool.Get<PooledPipelineContext>();

        // Initialize for full pipeline execution to avoid intermediate closures.
        runner.InitializeFull(snapshot, context, handler, ct);

        ValueTask pending = runner.RunAsync();
        if (pending.IsCompletedSuccessfully)
        {
            try
            {
#pragma warning disable CA1849 // Completed-success fast path; GetResult observes synchronous exceptions without blocking or allocating an async state machine.
                pending.GetAwaiter().GetResult();
#pragma warning restore CA1849
            }
            finally
            {
                this.ReturnRunnerSync(runner);
            }

            return ValueTask.CompletedTask;
        }

        return AwaitPendingAsync(this, pending, runner);

        static async ValueTask AwaitPendingAsync(MiddlewarePipeline<TPacket> owner, ValueTask operation, PooledPipelineContext pooledRunner)
        {
            try
            {
                await operation.ConfigureAwait(false);
            }
            finally
            {
                owner.ReturnRunnerSync(pooledRunner);
            }
        }
    }

    #endregion APIs

    #region Private Methods

    private static MiddlewareMetadata GetMiddlewareMetadata(Type middlewareType)
    {
        ArgumentNullException.ThrowIfNull(middlewareType);

        return s_metadataCache.GetOrAdd(middlewareType, static type =>
        {
            int order = 0;
            MiddlewareStage stage = MiddlewareStage.Inbound;
            bool alwaysExecute = false;

            if (Attribute.GetCustomAttribute(type, typeof(MiddlewareOrderAttribute))
                is MiddlewareOrderAttribute orderAttr)
            {
                order = orderAttr.Order;
            }

            if (Attribute.GetCustomAttribute(type, typeof(MiddlewareStageAttribute))
                is MiddlewareStageAttribute stageAttr)
            {
                stage = stageAttr.Stage;
                alwaysExecute = stageAttr.AlwaysExecute;
            }

            return new MiddlewareMetadata(order, stage, alwaysExecute);
        });
    }

    private static CancellationTokenSource? CreateExecutionToken(CancellationToken inboundToken, CancellationToken rootToken, out CancellationToken effectiveToken)
    {
        if (inboundToken.CanBeCanceled)
        {
            if (!rootToken.CanBeCanceled || inboundToken.Equals(rootToken))
            {
                effectiveToken = inboundToken;
                return null;
            }

            CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(inboundToken, rootToken);
            effectiveToken = linked.Token;
            return linked;
        }

        effectiveToken = rootToken;
        return null;
    }


    private PooledPipelineContext? AcquireRunner()
    {
        for (int i = 0; i < 32; i++)
        {
            long bit = 1L << i;
            if ((Interlocked.Read(ref _localPoolMask) & bit) == 0)
            {
                if ((Interlocked.Or(ref _localPoolMask, bit) & bit) == 0)
                {
                    ref PooledPipelineContext r = ref _localPool[i];
                    r ??= new PooledPipelineContext();
                    return r;
                }
            }
        }
        return null;
    }

    private void ReturnRunnerSync(PooledPipelineContext runner)
    {
        for (int i = 0; i < 32; i++)
        {
            if (ReferenceEquals(_localPool[i], runner))
            {
                long bit = 1L << i;
                _localPool[i].ResetForPool();
                _ = Interlocked.And(ref _localPoolMask, ~bit);
                return;
            }
        }

        // If it wasn't from our local pool, return to global.
        s_pool.Return<PooledPipelineContext>(runner);
    }

    private void ENSURE_SORTED_UNSAFE()
    {
        if (_isSorted)
        {
            return;
        }

        _inbound.Sort(static (a, b) => a.Order.CompareTo(b.Order));
        _outbound.Sort(static (a, b) => b.Order.CompareTo(a.Order));
        _outboundAlways.Sort(static (a, b) => b.Order.CompareTo(a.Order));
        _isSorted = true;
    }

    private void REBUILD_SNAPSHOT_UNSAFE()
    {
        this.ENSURE_SORTED_UNSAFE();

        MiddlewareEntry[] inbound = new MiddlewareEntry[_inbound.Count];
        for (int i = 0; i < inbound.Length; i++)
        {
            inbound[i] = _inbound[i];
        }

        MiddlewareEntry[] outbound = new MiddlewareEntry[_outbound.Count];
        for (int i = 0; i < outbound.Length; i++)
        {
            outbound[i] = _outbound[i];
        }

        MiddlewareEntry[] outboundAlways = new MiddlewareEntry[_outboundAlways.Count];
        for (int i = 0; i < outboundAlways.Length; i++)
        {
            outboundAlways[i] = _outboundAlways[i];
        }

        PipelineSnapshot snapshot = new(inbound, outbound, outboundAlways, _continueOnError, _errorHandler);

        Volatile.Write(ref _snapshot, snapshot);
    }

    #endregion Private Methods

    #region Nested Types

    private readonly record struct MiddlewareEntry(IPacketMiddleware<TPacket> Middleware, int Order);

    private readonly record struct MiddlewareMetadata(int Order, MiddlewareStage Stage, bool AlwaysExecute);

    private sealed class PipelineSnapshot
    {
        #region Static Fields

        public static readonly PipelineSnapshot Empty = new([], [], [], continueOnError: false, errorHandler: null);

        #endregion Static Fields

        #region Constructors

        public PipelineSnapshot(MiddlewareEntry[] inbound, MiddlewareEntry[] outbound, MiddlewareEntry[] outboundAlways, bool continueOnError, Action<Exception, Type>? errorHandler)
        {
            this.Inbound = inbound;
            this.Outbound = outbound;
            this.OutboundAlways = outboundAlways;
            this.ContinueOnError = continueOnError;
            this.ErrorHandler = errorHandler;
        }

        #endregion Constructors

        #region Properties

        public bool ContinueOnError { get; }

        public MiddlewareEntry[] Inbound { get; }

        public MiddlewareEntry[] Outbound { get; }

        public MiddlewareEntry[] OutboundAlways { get; }

        public Action<Exception, Type>? ErrorHandler { get; }

        public bool IsEmpty => this.Inbound.Length == 0 && this.Outbound.Length == 0 && this.OutboundAlways.Length == 0;

        #endregion Properties
    }

    private sealed class PooledPipelineContext : IPoolable
    {
        #region Fields

        private bool _continueOnError;
        private CancellationToken _startToken;
        private CancellationToken _rootCt;
        private PacketContext<TPacket>? _context;
        private MiddlewareEntry[] _middlewares = [];
        private Action<Exception, Type>? _errorHandler;

        private Func<CancellationToken, ValueTask>? _final;
        private Func<CancellationToken, ValueTask>[] _steps = [];

        // Full pipeline state
        private PipelineSnapshot? _snapshot;
        private PipelineStage _currentStage;
        private Func<CancellationToken, ValueTask>? _rootHandler;

        #endregion Fields

        #region APIs

        public void Initialize(
            MiddlewareEntry[] middlewares,
            PacketContext<TPacket> context,
            Func<CancellationToken, ValueTask> final,
            CancellationToken startToken,
            bool continueOnError,
            Action<Exception, Type>? errorHandler)
        {
            _middlewares = middlewares;
            _context = context;
            _final = final;
            _startToken = startToken;
            _continueOnError = continueOnError;
            _errorHandler = errorHandler;
            _currentStage = PipelineStage.Mid;
            this.ENSURE_STEPS(middlewares.Length + 1);
        }

        public void InitializeFull(
            PipelineSnapshot snapshot,
            PacketContext<TPacket> context,
            Func<CancellationToken, ValueTask> handler,
            CancellationToken ct)
        {
            _snapshot = snapshot;
            _context = context;
            _rootHandler = handler;
            _rootCt = ct;
            _startToken = ct;
            _continueOnError = snapshot.ContinueOnError;
            _errorHandler = snapshot.ErrorHandler;

            this.SET_STAGE(PipelineStage.Inbound);
        }

        public void ResetForPool()
        {
            _middlewares = [];
            _context = null;
            _final = null;
            _startToken = default;
            _rootCt = default;
            _continueOnError = false;
            _errorHandler = null;
            _snapshot = null;
            _rootHandler = null;
            _currentStage = PipelineStage.None;
        }

        public ValueTask RunAsync() => _steps[0](_startToken);

        #endregion APIs

        #region Private Methods

        private void SET_STAGE(PipelineStage stage)
        {
            _currentStage = stage;
            _middlewares = stage switch
            {
                PipelineStage.Inbound => _snapshot!.Inbound,
                PipelineStage.OutboundAlways => _snapshot!.OutboundAlways,
                PipelineStage.Outbound => _snapshot!.Outbound,
                PipelineStage.None => throw new NotImplementedException(),
                PipelineStage.Mid => throw new NotImplementedException(),
                PipelineStage.Finished => throw new NotImplementedException(),
                _ => []
            };

            this.ENSURE_STEPS(_middlewares.Length + 1);
        }

        private void ENSURE_STEPS(int requiredLength)
        {
            if (_steps.Length >= requiredLength)
            {
                return;
            }

            int currentLength = _steps.Length;
            Array.Resize(ref _steps, requiredLength);

            for (int i = currentLength; i < requiredLength; i++)
            {
                int index = i;
                _steps[i] = token => this.INVOKE_ASYNC(index, token);
            }
        }

        private ValueTask INVOKE_ASYNC(int index, CancellationToken token)
        {
            if ((uint)index >= (uint)_middlewares.Length)
            {
                // Last step in current stage
                if (_currentStage == PipelineStage.Mid)
                {
                    Func<CancellationToken, ValueTask>? final = _final;
                    return final is null ? ValueTask.CompletedTask : final(token);
                }

                return this.TRANSITION_ASYNC(token);
            }

            PacketContext<TPacket>? context = _context;
            if (context is null)
            {
                return ValueTask.FromException(
                    new InternalErrorException("Middleware pipeline runner is not initialized."));
            }

            MiddlewareEntry entry = _middlewares[index];
            Func<CancellationToken, ValueTask> next = _steps[index + 1];

            try
            {
                ValueTask pending = entry.Middleware.InvokeAsync(context, next);
                if (!_continueOnError || pending.IsCompletedSuccessfully)
                {
                    return pending;
                }

                return AwaitWithContinueAsync(this, pending, entry, next, token);
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                if (!_continueOnError)
                {
                    return ValueTask.FromException(ex);
                }

                _errorHandler?.Invoke(ex, entry.Middleware.GetType());
                return next(token);
            }
        }

        private async ValueTask TRANSITION_ASYNC(CancellationToken token)
        {
            switch (_currentStage)
            {
                case PipelineStage.Inbound:
                    await this.EXECUTE_HANDLER_FULL_ASYNC(token).ConfigureAwait(false);
                    break;

                case PipelineStage.OutboundAlways:
                    if (!_context!.SkipOutbound && !token.IsCancellationRequested)
                    {
                        this.SET_STAGE(PipelineStage.Outbound);
                        await this.RunAsync().ConfigureAwait(false);
                    }
                    else
                    {
                        _currentStage = PipelineStage.Finished;
                    }
                    break;

                case PipelineStage.Outbound:
                    _currentStage = PipelineStage.Finished;
                    break;
                case PipelineStage.None:
                    break;
                case PipelineStage.Mid:
                    break;
                case PipelineStage.Finished:
                    break;
                default:
                    break;
            }
        }

        private async ValueTask EXECUTE_HANDLER_FULL_ASYNC(CancellationToken inboundCt)
        {
            CancellationTokenSource? linkedCts = null;
            try
            {
                linkedCts = CreateExecutionToken(inboundCt, _rootCt, out CancellationToken handlerCt);

                try
                {
                    ValueTask handlerPending = _rootHandler!(handlerCt);
                    if (!handlerPending.IsCompletedSuccessfully)
                    {
                        await handlerPending.ConfigureAwait(false);
                    }
                    else
                    {
#pragma warning disable CA1849 // Completed-success fast path; GetResult observes synchronous exceptions without blocking or allocating an async state machine.
                        handlerPending.GetAwaiter().GetResult();
#pragma warning restore CA1849
                    }
                }
                catch (OperationCanceledException) when (handlerCt.IsCancellationRequested)
                {
                }

                this.SET_STAGE(PipelineStage.OutboundAlways);
                await this.RunAsync().ConfigureAwait(false);
            }
            finally
            {
                linkedCts?.Dispose();
            }
        }

        private static async ValueTask AwaitWithContinueAsync(
            PooledPipelineContext runner,
            ValueTask pending,
            MiddlewareEntry entry,
            Func<CancellationToken, ValueTask> next,
            CancellationToken token)
        {
            try
            {
                await pending.ConfigureAwait(false);
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                runner._errorHandler?.Invoke(ex, entry.Middleware.GetType());
                await next(token).ConfigureAwait(false);
            }
        }

        #endregion Private Methods

        private enum PipelineStage
        {
            None,
            Inbound,
            Mid,
            OutboundAlways,
            Outbound,
            Finished
        }
    }

    #endregion Nested Types
}
