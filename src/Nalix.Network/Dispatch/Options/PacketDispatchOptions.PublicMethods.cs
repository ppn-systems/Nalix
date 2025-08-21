// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Connection;
using Nalix.Common.Logging.Abstractions;
using Nalix.Common.Packets.Attributes;
using Nalix.Network.Abstractions;
using Nalix.Network.Dispatch.Delegates;
using Nalix.Network.Internal.Dispatch;
using Nalix.Shared.Injection;
using Nalix.Shared.Memory.Pooling;

namespace Nalix.Network.Dispatch.Options;

public sealed partial class PacketDispatchOptions<TPacket>
{
    /// <summary>
    /// Configures logging for the packet dispatcher, enabling logging of packet processing details.
    /// </summary>
    /// <param name="logger">The logger instance that will be used for logging packet processing events.</param>
    /// <returns>
    /// The current <see cref="PacketDispatchOptions{TPacket}"/> instance for method chaining.
    /// </returns>
    /// <remarks>
    /// The logger will be used to log various events such as packet handling, errors, and metrics if enabled.
    /// If logging is not configured, the dispatcher will not produce any logs.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public PacketDispatchOptions<TPacket> WithLogging(ILogger logger)
    {
        this.Logger = logger;
        this.Logger.Info("Logger instance successfully attached to PacketDispatch. Logging is now active.");

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
    /// <remarks>
    /// This method allows you to define a custom error-handling strategy, such as logging errors,
    /// sending notifications, or taking corrective action in case of failures during packet processing.
    /// If no custom error handler is configured, the default behavior is to log the exception.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
         System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public PacketDispatchOptions<TPacket> WithErrorHandling(
        System.Action<System.Exception, System.UInt16> errorHandler)
    {
        this.Logger?.Info("Custom error handler has been set. All unhandled exceptions during packet processing will be routed.");
        this._errorHandler = errorHandler;

        return this;
    }

    /// <summary>
    /// Adds a middleware component to the beginning of the packet processing pipeline.
    /// </summary>
    /// <param name="middleware">
    /// The <see cref="IPacketMiddleware{TPacket}"/> instance that will be invoked before the main packet handler.
    /// </param>
    /// <returns>
    /// The current <see cref="PacketDispatchOptions{TPacket}"/> instance for method chaining.
    /// </returns>
    /// <remarks>
    /// PreDispatch-processing middleware allows for custom logic such as validation, authorization, logging,
    /// or modification of packet data before it reaches the main handler. Middleware is executed in the
    /// order it is added.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public PacketDispatchOptions<TPacket> WithPreDispatchMiddleware(
        IPacketMiddleware<TPacket> middleware)
    {
        _ = this._pipeline.UsePre(middleware);
        return this;
    }

    /// <summary>
    /// Adds a middleware component to the end of the packet processing pipeline.
    /// </summary>
    /// <param name="middleware">
    /// The <see cref="IPacketMiddleware{TPacket}"/> instance that will be invoked after the main packet handler completes.
    /// </param>
    /// <returns>
    /// The current <see cref="PacketDispatchOptions{TPacket}"/> instance for method chaining.
    /// </returns>
    /// <remarks>
    /// PostDispatch-processing middleware is useful for tasks such as auditing, cleanup, metrics collection,
    /// or response transformation. Middleware is executed in the order it is added.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public PacketDispatchOptions<TPacket> WithPostDispatchMiddleware(
        IPacketMiddleware<TPacket> middleware)
    {
        _ = this._pipeline.UsePost(middleware);
        return this;
    }

    /// <summary>
    /// Registers a handler by creating an instance of the specified controller type
    /// and scanning its methods decorated with <see cref="PacketOpcodeAttribute"/>.
    /// </summary>
    /// <typeparam name="TController">
    /// The type of the controller to register.
    /// This type must have a parameterless constructor.
    /// </typeparam>
    /// <returns>The current <see cref="PacketDispatchOptions{TPacket}"/> instance for chaining.</returns>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown if a method with an unsupported return type is encountered.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public PacketDispatchOptions<TPacket> WithHandler<[
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicMethods |
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TController>()
        where TController : class, new()
        => this.WithHandler(() => new TController());

    /// <summary>
    /// Registers a handler using an existing instance of the specified controller type.
    /// </summary>
    /// <typeparam name="TController">
    /// The type of the controller to register.
    /// </typeparam>
    /// <param name="instance">
    /// An existing instance of <typeparamref name="TController"/>.
    /// </param>
    /// <returns>
    /// The current <see cref="PacketDispatchOptions{TPacket}"/> instance for chaining.
    /// </returns>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown if <paramref name="instance"/> is <see langword="null"/>.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public PacketDispatchOptions<TPacket> WithHandler<[
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicMethods |
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TController>(TController instance)
        where TController : class
        => this.WithHandler(() => EnsureNotNull(instance, nameof(instance)));

    /// <summary>
    /// Registers a handler by creating an instance of the specified controller type
    /// using a provided factory function, then scanning its methods decorated
    /// with <see cref="PacketOpcodeAttribute"/>.
    /// </summary>
    /// <typeparam name="TController">
    /// The type of the controller to register. This type does not require
    /// a parameterless constructor.
    /// </typeparam>
    /// <param name="factory">
    /// A function that returns an instance of <typeparamref name="TController"/>.
    /// </param>
    /// <returns>The current <see cref="PacketDispatchOptions{TPacket}"/> instance for chaining.</returns>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown if a method with an unsupported return type is encountered.
    /// </exception>
    public PacketDispatchOptions<TPacket> WithHandler<
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicMethods)] TController>(
        System.Func<TController> factory)
        where TController : class
    {
        System.Type controllerType = typeof(TController);

        PacketControllerAttribute controllerAttr = System.Reflection.CustomAttributeExtensions
            .GetCustomAttribute<PacketControllerAttribute>(controllerType)
            ?? throw new System.InvalidOperationException(
                $"The controller '{controllerType.Name}' is missing the [PacketController] attribute.");

        PacketHandler<TPacket>[] handlerDescriptors =
            PacketAnalyzer<TController, TPacket>.ScanController(factory);

        foreach (PacketHandler<TPacket> descriptor in handlerDescriptors)
        {
            if (this._handlerCache.ContainsKey(descriptor.OpCode))
            {
                throw new System.InvalidOperationException(
                    $"OpCode '{descriptor.OpCode}' has already been registered.");
            }

            this._handlerCache[descriptor.OpCode] = descriptor;
        }

        this.Logger?.Info($"Registered {handlerDescriptors.Length} handlers for controller {controllerType.Name}");

        return this;
    }

    /// <summary>
    /// Legacy compatibility method - wraps new descriptor-based handler in the old signature.
    /// Maintained for backward compatibility with existing code.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean TryResolveHandler(
        System.UInt16 opCode,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)]
        out System.Func<TPacket, IConnection, System.Threading.Tasks.Task>? handler)
    {
        if (TryResolveHandlerDescriptor(opCode,
            out PacketHandler<TPacket> descriptor))
        {
            handler = async (packet, connection) =>
            {
                PacketContext<TPacket> context = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                                                         .Get<PacketContext<TPacket>>();

                try
                {
                    context.Initialize(packet, connection, descriptor.Attributes);
                    await this.ExecuteHandlerAsync(descriptor, context)
                              .ConfigureAwait(false);
                }
                finally
                {
                    context.Return();
                }
            };

            return true;
        }

        handler = null;
        return false;
    }

    /// <summary>
    /// New preferred method - returns descriptor directly for better performance.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean TryResolveHandlerDescriptor(
        System.UInt16 opCode,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)]
        out PacketHandler<TPacket> descriptor)
    {
        if (this._handlerCache.TryGetValue(opCode, out descriptor))
        {
            return true;
        }

        this.Logger?.Warn("Handler not found for OpCode={0}", opCode);
        return false;
    }
}
