using Nalix.Common.Packets;
using Nalix.Network.Dispatch.Core;
using Nalix.Network.Dispatch.Internal.ReturnTypes;
using Nalix.Network.Dispatch.Middleware;

namespace Nalix.Network.Dispatch.Options;
public sealed partial class PacketDispatchOptions<TPacket> where TPacket : IPacket, IPacketTransformer<TPacket>
{
    #region Private Methods

    /// <summary>
    /// Configure default middleware pipeline.
    /// </summary>
    private void ConfigureDefaultMiddleware()
    {
        // Pre-processing middleware
        _ = this._pipeline
            .UsePre(new RateLimitMiddleware<TPacket>())
            .UsePre(new PermissionMiddleware<TPacket>())
            .UsePre(new PacketTransformMiddleware<TPacket>());
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
        PacketHandlerDelegate<TPacket> descriptor,
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
            IReturnHandler<TPacket> returnHandler = ReturnTypeHandlerFactory<TPacket>.GetHandler(descriptor.ReturnType);
            await returnHandler.HandleAsync(result, context);
        }
        catch (System.Exception ex)
        {
            await this.HandleExecutionException(descriptor, context, ex);
        }
    }

    /// <summary>
    /// Handle execution exception.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private async System.Threading.Tasks.ValueTask HandleExecutionException(
        PacketHandlerDelegate<TPacket> descriptor,
        PacketContext<TPacket> context,
        System.Exception exception)
    {
        this._logger?.Error("Handler execution failed for OpCode={0}: {1}",
            descriptor.OpCode, exception.Message);

        this._errorHandler?.Invoke(exception, descriptor.OpCode);

        _ = await context.Connection.Tcp.SendAsync(TPacket.Create(0, "Internal server error"));
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static T EnsureNotNull<T>(T value, System.String paramName) where T : class
        => value ?? throw new System.ArgumentNullException(paramName);

    #endregion Private Methods
}
