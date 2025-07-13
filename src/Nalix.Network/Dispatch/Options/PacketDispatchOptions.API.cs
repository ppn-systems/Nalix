using Nalix.Common.Connection;
using Nalix.Common.Logging;
using Nalix.Common.Package.Attributes;
using Nalix.Network.Dispatch.Core;
using Nalix.Network.Dispatch.Middleware;
using Nalix.Shared.Memory.Pooling;

namespace Nalix.Network.Dispatch.Options;

public sealed partial class PacketDispatchOptions<TPacket>
{
    /// <summary>
    /// Configure logging cho packet dispatcher.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public PacketDispatchOptions<TPacket> WithLogging(ILogger logger)
    {
        _logger = logger;
        _logger.Info("Logging has been enabled for PacketDispatch.");
        return this;
    }

    /// <summary>
    /// Configure custom error handling.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public PacketDispatchOptions<TPacket> WithErrorHandling(
        System.Action<System.Exception, System.UInt16> errorHandler)
    {
        _errorHandler = errorHandler;
        _logger?.Info("Custom error handler has been configured.");
        return this;
    }

    /// <summary>
    /// Add custom middleware vào pipeline.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public PacketDispatchOptions<TPacket> WithMiddleware(
        IPacketMiddleware<TPacket> middleware)
    {
        _pipeline.UsePre(middleware);
        return this;
    }

    /// <summary>
    /// Add post-processing middleware.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public PacketDispatchOptions<TPacket> WithPostMiddleware(
        IPacketMiddleware<TPacket> middleware)
    {
        _pipeline.UsePost(middleware);
        return this;
    }

    /// <summary>
    /// Registers a handler by creating an instance of the specified controller type
    /// and scanning its methods decorated with PacketOpcodeAttribute.
    /// </summary>
    /// <typeparam name="TController">
    /// The type of the controller to register.
    /// This type must have a parameterless constructor.
    /// </typeparam>
    /// <returns>The current PacketDispatchOptions instance for chaining.</returns>
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
    /// <typeparam name="TController">The type of the controller to register.</typeparam>
    /// <param name="instance">An existing instance of TController.</param>
    /// <returns>The current PacketDispatchOptions instance for chaining.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public PacketDispatchOptions<TPacket> WithHandler<[
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicMethods |
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TController>(TController instance)
        where TController : class
        => this.WithHandler(() => EnsureNotNull(instance, nameof(instance)));

    /// <summary>
    /// Core handler registration với factory pattern.
    /// Đây là method chính để đăng ký handlers.
    /// </summary>
    /// <typeparam name="TController">Controller type</typeparam>
    /// <param name="factory">Factory function để tạo controller instance</param>
    /// <returns>PacketDispatchOptions for chaining</returns>
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

        PacketAnalyzer<TController, TPacket> scanner = new(_logger);
        PacketHandlerInvoker<TPacket>[] handlerDescriptors = scanner.ScanController(factory);

        foreach (PacketHandlerInvoker<TPacket> descriptor in handlerDescriptors)
        {
            if (_handlerCache.ContainsKey(descriptor.OpCode))
            {
                throw new System.InvalidOperationException(
                    $"OpCode '{descriptor.OpCode}' has already been registered.");
            }

            _handlerCache[descriptor.OpCode] = descriptor;
        }

        _logger?.Info("Registered {0} handlers for controller {1}",
            handlerDescriptors.Length, controllerType.Name);

        return this;
    }

    /// <summary>
    /// Legacy compatibility method - wraps new descriptor-based handler trong old signature.
    /// Maintained để backward compatibility với existing code.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public bool TryResolveHandler(
        System.UInt16 opCode,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)]
        out System.Func<TPacket, IConnection, System.Threading.Tasks.Task>? handler)
    {
        if (_handlerCache.TryGetValue(opCode, out var descriptor))
        {
            // Wrap descriptor execution trong legacy Task-based signature
            handler = async (packet, connection) =>
            {
                // Rent context from pool
                PacketContext<TPacket> context = ObjectPoolManager.Instance.Get<PacketContext<TPacket>>();

                try
                {
                    // Initialize context
                    context.Initialize(packet, connection, descriptor.Attributes);

                    // Execute through new pipeline
                    await ExecuteHandler(descriptor, context);
                }
                finally
                {
                    ObjectPoolManager.Instance.Return<PacketContext<TPacket>>(context);
                }
            };

            return true;
        }

        _logger?.Warn("Handler not found for OpCode={0}", opCode);
        handler = null;
        return false;
    }

    /// <summary>
    /// New preferred method - returns descriptor directly cho better performance.
    /// Sử dụng method này cho new code để có performance tối ưu.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public bool TryResolveHandlerDescriptor(
        System.UInt16 opCode,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)]
        out PacketHandlerInvoker<TPacket> descriptor)
    {
        if (_handlerCache.TryGetValue(opCode, out descriptor))
            return true;

        _logger?.Warn("Handler not found for OpCode={0}", opCode);
        return false;
    }
}