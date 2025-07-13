using Nalix.Common.Logging;
using Nalix.Common.Package;
using Nalix.Network.Configurations;
using Nalix.Network.Dispatch.Core;
using Nalix.Network.Dispatch.Middleware;
using Nalix.Network.Dispatch.Middleware.Inbound;
using Nalix.Network.Dispatch.ReturnHandlers;
using Nalix.Shared.Configuration;

namespace Nalix.Network.Dispatch.Options;

/// <summary>
/// Provides options for packet dispatching, including middleware configuration,
/// error handling, and logging.
/// </summary>
/// <typeparam name="TPacket">The type of packet being dispatched.</typeparam>
public sealed partial class PacketDispatchOptions<TPacket> where TPacket : IPacket,
    IPacketFactory<TPacket>,
    IPacketEncryptor<TPacket>,
    IPacketCompressor<TPacket>
{
    #region Fields

    private ILogger? _logger;

    /// <summary>
    /// Gets or sets a custom error-handling delegate invoked when packet processing fails.
    /// </summary>
    /// <remarks>
    /// If not set, exceptions are only logged. You can override this to trigger alerts or retries.
    /// </remarks>
    private System.Action<System.Exception, System.UInt16>? _errorHandler;

    private readonly System.Collections.Generic.Dictionary<
        System.UInt16, PacketHandlerInvoker<TPacket>> _handlerCache;

    private readonly PacketMiddlewarePipeline<TPacket> _pipeline;

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketDispatchOptions{TPacket}"/> class.
    /// </summary>
    public PacketDispatchOptions()
    {
        _handlerCache = [];
        _pipeline = new PacketMiddlewarePipeline<TPacket>();
    }

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the logger instance used for logging within the packet dispatch options.
    /// </summary>
    public ILogger? Logger => _logger;

    /// <summary>
    /// Gets the dispatch queue options.
    /// </summary>
    public readonly DispatchQueueOptions QueueOptions = ConfigurationStore.Instance.Get<DispatchQueueOptions>();

    #endregion Properties

    #region Private Methods

    /// <summary>
    /// Configure default middleware pipeline.
    /// </summary>
    private void ConfigureDefaultMiddleware()
    {
        // Pre-processing middleware
        _pipeline
            .UsePre(new RateLimitMiddleware<TPacket>())
            .UsePre(new DecompressionMiddleware<TPacket>())
            .UsePre(new DecryptionMiddleware<TPacket>());
        //.UsePre(new ValidationMiddleware<TPacket>());

        // Post-processing middleware
        //_pipeline
        //    .UsePost(new CompressionMiddleware<TPacket>())
        //    .UsePost(new EncryptionMiddleware<TPacket>())
        //    .UsePost(new LoggingMiddleware<TPacket>(_logger));
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private async System.Threading.Tasks.ValueTask ExecuteHandler(
        PacketHandlerInvoker<TPacket> descriptor,
        PacketContext<TPacket> context)
    {
        // Validation check
        //if (!descriptor.CanExecute(context))
        //{
        //}

        try
        {
            // Execute compiled handler
            System.Object? result = await descriptor.ExecuteAsync(context);

            // Handle return value
            IPacketReturnHandler<TPacket> returnHandler = ReturnTypeHandlerFactory<TPacket>.GetHandler(descriptor.ReturnType);
            await returnHandler.HandleAsync(result, context);
        }
        catch (System.Exception ex)
        {
            await HandleExecutionException(descriptor, context, ex);
        }
    }

    /// <summary>
    /// Handle execution exception.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private async System.Threading.Tasks.ValueTask HandleExecutionException(
        PacketHandlerInvoker<TPacket> descriptor,
        PacketContext<TPacket> context,
        System.Exception exception)
    {
        _logger?.Error("Handler execution failed for OpCode={0}: {1}",
            descriptor.OpCode, exception.Message);

        _errorHandler?.Invoke(exception, descriptor.OpCode);

        TPacket errorPacket = TPacket.Create(0, "Internal server error");
        await context.Connection.Tcp.SendAsync(errorPacket.Serialize());
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static T EnsureNotNull<T>(T value, string paramName) where T : class
        => value ?? throw new System.ArgumentNullException(paramName);

    #endregion Private Methods
}