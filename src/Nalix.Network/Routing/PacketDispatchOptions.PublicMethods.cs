// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nalix.Common.Exceptions;
using Nalix.Common.Middleware;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Network.Internal.Compilation;
using Nalix.Network.Internal.Results;

namespace Nalix.Network.Routing;

public sealed partial class PacketDispatchOptions<TPacket>
{
    /// <summary>
    /// Configures logging for the packet dispatcher, enabling logging of packet processing details.
    /// </summary>
    /// <param name="logger">The logger instance that will be used for logging packet processing events.</param>
    /// <returns>
    /// The current <see cref="PacketDispatchOptions{TPacket}"/> instance for method chaining.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public PacketDispatchOptions<TPacket> WithLogging(ILogger logger)
    {
        this.Logging = logger;
        this.Logging.Debug($"[NW.{nameof(PacketDispatchOptions<>)}:{nameof(WithLogging)}] logger-attached");

        return this;
    }

    /// <summary>
    /// Configures a custom error handler to manage exceptions during packet processing.
    /// </summary>
    /// <param name="errorHandler">
    /// An action that takes an exception and the packet ID, which allows custom handling of errors
    /// that occur while processing packets.
    /// </param>
    /// <returns>
    /// The current <see cref="PacketDispatchOptions{TPacket}"/> instance for method chaining.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public PacketDispatchOptions<TPacket> WithErrorHandling(
        Action<Exception, ushort> errorHandler)
    {
        this.Logging?.Debug($"[NW.{nameof(PacketDispatchOptions<>)}:{nameof(WithErrorHandling)}] error-handler-set");
        _errorHandler = errorHandler;

        return this;
    }

    /// <summary>
    /// Adds a middleware component to the packet processing pipeline.
    /// </summary>
    /// <param name="middleware">
    /// The <see cref="IPacketMiddleware{TPacket}"/> instance that will be invoked during packet processing.
    /// </param>
    /// <returns>
    /// The current <see cref="PacketDispatchOptions{TPacket}"/> instance for method chaining.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public PacketDispatchOptions<TPacket> WithMiddleware(IPacketMiddleware<TPacket> middleware)
    {
        ArgumentNullException.ThrowIfNull(middleware);

        this.Logging?.Debug($"[NW.{nameof(PacketDispatchOptions<>)}:{nameof(WithMiddleware)}] middleware-added type={middleware.GetType().Name}");

        _pipeline.Use(middleware);

        return this;
    }

    /// <summary>
    /// Adds a middleware component to the packet processing pipeline.
    /// </summary>
    /// <param name="middleware">
    /// The <see cref="IPacketMiddleware{TPacket}"/> instance that will be invoked during packet processing.
    /// </param>
    /// <returns>
    /// The current <see cref="PacketDispatchOptions{TPacket}"/> instance for method chaining.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public PacketDispatchOptions<TPacket> WithBufferMiddleware(INetworkBufferMiddleware middleware)
    {
        ArgumentNullException.ThrowIfNull(middleware);

        this.Logging?.Debug($"[NW.{nameof(PacketDispatchOptions<>)}:{nameof(WithMiddleware)}] middleware-added type={middleware.GetType().Name}");

        this.NetworkPipeline.Use(middleware);

        return this;
    }

    /// <summary>
    /// Overrides the default number of dispatch loops.
    /// </summary>
    /// <param name="loopCount">
    /// The number of worker loops to start. Must be between 1 and 64.
    /// If <see langword="null"/>, the dispatcher chooses automatically based on the host CPU.
    /// </param>
    /// <returns>The current options instance.</returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public PacketDispatchOptions<TPacket> WithDispatchLoopCount(int? loopCount)
    {
        if (loopCount.HasValue && (loopCount.Value < 1 || loopCount.Value > 64))
        {
            throw new ArgumentOutOfRangeException(
                nameof(loopCount),
                "Dispatch loop count must be between 1 and 64.");
        }

        this.DispatchLoopCount = loopCount;
        this.Logging?.Debug($"[NW.{nameof(PacketDispatchOptions<>)}:{nameof(WithDispatchLoopCount)}] loops={(loopCount.HasValue ? loopCount.Value.ToString(CultureInfo.InvariantCulture) : "auto")}");
        return this;
    }

    /// <summary>
    /// Configures how the middleware pipeline handles exceptions thrown during packet processing.
    /// </summary>
    /// <param name="continueOnError">
    /// A value indicating whether the pipeline should continue processing subsequent middleware
    /// when an exception occurs. If <see langword="true"/>, execution continues; otherwise, the
    /// pipeline is terminated.
    /// </param>
    /// <param name="errorHandler">
    /// An optional delegate that is invoked when an exception is thrown. The delegate receives
    /// the <see cref="Exception"/> instance and the <see cref="Type"/> of the packet
    /// being processed. If <see langword="null"/>, no custom error handling is applied.
    /// </param>
    /// <returns>
    /// The current <see cref="PacketDispatchOptions{TPacket}"/> instance, allowing for method chaining.
    /// </returns>
    /// <remarks>
    /// This method allows fine-grained control over error handling behavior in the middleware pipeline.
    /// Use <paramref name="continueOnError"/> with caution, as continuing after exceptions may lead
    /// to inconsistent state depending on middleware implementation.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown if required dependencies within the pipeline are not initialized.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public PacketDispatchOptions<TPacket> WithErrorHandlingMiddleware(
        bool continueOnError,
        Action<Exception, Type>? errorHandler = null)
    {
        _pipeline.ConfigureErrorHandling(continueOnError, errorHandler);
        return this;
    }

    /// <summary>
    /// Registers a handler by creating an instance of the specified controller type
    /// and scanning its methods decorated with <see cref="PacketOpcodeAttribute"/>.
    /// </summary>
    /// <typeparam name="TController">
    /// The type of the controller to register. Must have a parameterless constructor.
    /// </typeparam>
    /// <returns>The current <see cref="PacketDispatchOptions{TPacket}"/> instance for chaining.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public PacketDispatchOptions<TPacket> WithHandler<[
        DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicMethods |
            DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TController>()
        where TController : class, new() => this.WithHandler(() => new TController());

    /// <summary>
    /// Registers a handler using an existing instance of the specified controller type.
    /// </summary>
    /// <typeparam name="TController">The type of the controller to register.</typeparam>
    /// <param name="instance">An existing instance of <typeparamref name="TController"/>.</param>
    /// <returns>
    /// The current <see cref="PacketDispatchOptions{TPacket}"/> instance for chaining.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public PacketDispatchOptions<TPacket> WithHandler<[
        DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicMethods |
            DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TController>(
        TController instance)
        where TController : class => this.WithHandler(() => ThrowIfNull(instance, nameof(instance)));

    /// <summary>
    /// Registers a handler by creating an instance of the specified controller type
    /// using a provided factory function, then scanning its methods decorated
    /// with <see cref="PacketOpcodeAttribute"/>.
    /// </summary>
    /// <typeparam name="TController">
    /// The type of the controller to register.
    /// </typeparam>
    /// <param name="factory">
    /// A function that returns an instance of <typeparamref name="TController"/>.
    /// </param>
    /// <returns>The current <see cref="PacketDispatchOptions{TPacket}"/> instance for chaining.</returns>
    /// <remarks>
    /// <para>
    /// In addition to compiling handler delegates, this method inspects each handler method's
    /// first parameter to determine the concrete packet type it expects. That type is stored in
    /// <c>_packetTypeMap[opCode]</c> and consulted at dispatch time to emit an early, actionable
    /// warning when the deserialized packet's runtime type does not match the handler signature —
    /// rather than a cryptic <see cref="InvalidCastException"/> deep inside an expression tree.
    /// </para>
    /// <para>
    /// For context-style handlers whose first parameter is <c>PacketContext&lt;TPacket&gt;</c>,
    /// no concrete-type entry is recorded because the context wraps the packet behind its
    /// interface — the cast is deferred to handler code itself.
    /// </para>
    /// </remarks>
    /// <exception cref="InternalErrorException"></exception>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public PacketDispatchOptions<TPacket> WithHandler<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] TController>(Func<TController> factory)
        where TController : class
    {
        ArgumentNullException.ThrowIfNull(factory);

        Type controllerType = typeof(TController);

        PacketControllerAttribute controllerAttr = CustomAttributeExtensions.GetCustomAttribute<PacketControllerAttribute>(controllerType)
            ?? throw new InternalErrorException($"The controller '{controllerType.Name}' is missing the [PacketController] attribute.");

        PacketHandler<TPacket>[] compiledHandlers = PacketHandlerCompiler<TController, TPacket>.CompileHandlers(factory);

        Type contextType = typeof(PacketContext<TPacket>);

        for (int i = 0; i < compiledHandlers.Length; i++)
        {
            PacketHandler<TPacket> descriptor = compiledHandlers[i];
            int index = descriptor.OpCode;

            if (Volatile.Read(ref _handlerFlags[index]) != 0)
            {
                throw new InternalErrorException($"OpCode '{descriptor.OpCode}' has already been registered.");
            }

            Type? concretePacketType = ResolveConcretePacketType(descriptor.MethodInfo, contextType);
            IReturnHandler<TPacket> returnHandler = ReturnTypeHandlerFactory<TPacket>.ResolveHandler(descriptor.ReturnType);

            PacketHandler<TPacket> runtimeHandler = new(
                descriptor.OpCode,
                descriptor.Metadata,
                descriptor.Instance,
                descriptor.MethodInfo,
                descriptor.ReturnType,
                descriptor.Invoker,
                concretePacketType,
                returnHandler);

            _handlerTable[index] = runtimeHandler;
            Volatile.Write(ref _handlerFlags[index], 1);
            _ = Interlocked.Increment(ref _handlerCount);

            if (concretePacketType is not null && concretePacketType != typeof(TPacket))
            {
                this.Logging?.Debug(
                    $"[NW.{nameof(PacketDispatchOptions<>)}:{nameof(WithHandler)}] " +
                    $"type-map opcode=0x{descriptor.OpCode:X4} -> {concretePacketType.Name}");
            }
        }

        this.Logging?.Info($"[NW.{nameof(PacketDispatchOptions<>)}:{nameof(WithHandler)}] " +
                           $"reg-handlers count={compiledHandlers.Length} controller={controllerType.Name}");

        return this;
    }

    /// <summary>
    /// Attempts to resolve a registered packet handler descriptor for the specified opcode.
    /// </summary>
    /// <param name="opCode">The opcode of the packet handler to resolve.</param>
    /// <param name="handler">
    /// When this method returns, contains the cached handler descriptor associated with
    /// <paramref name="opCode"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if a handler descriptor was found; otherwise <see langword="false"/>.
    /// </returns>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal bool TryResolveHandler(ushort opCode, out PacketHandler<TPacket> handler)
    {
        if (Volatile.Read(ref _handlerFlags[opCode]) != 0)
        {
            handler = _handlerTable[opCode];
            return true;
        }

        handler = default;
        return false;
    }

    /// <summary>
    /// Executes a resolved handler with a pooled packet context.
    /// </summary>
    /// <param name="descriptor">Resolved handler descriptor.</param>
    /// <param name="packet">Incoming packet.</param>
    /// <param name="connection">Source connection.</param>
    /// <param name="token">Cancellation token for dispatch.</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal ValueTask ExecuteResolvedHandlerAsync(
        in PacketHandler<TPacket> descriptor,
        TPacket packet,
        IConnection connection,
        CancellationToken token = default)
    {
        PacketContext<TPacket> context = _objectPool.Get<PacketContext<TPacket>>();
        try
        {
            context.Initialize(packet, connection, descriptor.Metadata, token);
        }
        catch
        {
            context.Return();
            throw;
        }

        ValueTask pending = this.ExecuteHandlerAsync(descriptor, context);
        if (pending.IsCompletedSuccessfully)
        {
            try
            {
                pending.GetAwaiter().GetResult();
            }
            finally
            {
                context.Return();
            }

            return ValueTask.CompletedTask;
        }

        return AwaitAndReturnAsync(pending, context);

        static async ValueTask AwaitAndReturnAsync(ValueTask operation, PacketContext<TPacket> pooledContext)
        {
            try
            {
                await operation.ConfigureAwait(false);
            }
            finally
            {
                pooledContext.Return();
            }
        }
    }
}
