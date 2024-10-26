using Nalix.Common.Packets;
using Nalix.Network.Dispatch.Core;
using Nalix.Network.Dispatch.Internal.ReturnTypes;
using System;

namespace Nalix.Network.Dispatch.Options;
public sealed partial class PacketDispatchOptions<TPacket> where TPacket : IPacket, IPacketTransformer<TPacket>
{
    #region Private Methods

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
    /// Handle timeout exception.
    /// </summary>
    private async System.Threading.Tasks.ValueTask HandleTimeout(
        PacketHandlerDelegate<TPacket> descriptor,
        PacketContext<TPacket> context)
    {
        this._logger?.Warn("Handler timeout for OpCode={0}", descriptor.OpCode);

        this._errorHandler?.Invoke(new TimeoutException("Handler timeout"), descriptor.OpCode);

        _ = await context.Connection.Tcp.SendAsync(
            TPacket.Create(0, "Request timed out"));
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
