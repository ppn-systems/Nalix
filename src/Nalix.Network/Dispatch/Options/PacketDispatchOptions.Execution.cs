using Nalix.Common.Packets.Interfaces;
using Nalix.Network.Dispatch.Core;
using Nalix.Network.Dispatch.ReturnTypes;
using Nalix.Network.Protocols.Messages;
using Nalix.Shared.Memory.Pooling;

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
        try
        {
            System.Int32 timeout = descriptor.Attributes.Timeout?.TimeoutMilliseconds ?? 0;
            System.Threading.Tasks.ValueTask<System.Object?> execTask = descriptor.ExecuteAsync(context);

            if (timeout > 0)
            {
                var execTaskAsTask = execTask.AsTask(); // Convert ValueTask to Task once
                var completed = await System.Threading.Tasks.Task.WhenAny(
                    execTaskAsTask, System.Threading.Tasks.Task.Delay(timeout));

                if (completed != execTaskAsTask)
                {
                    LiteralPacket text = ObjectPoolManager.Instance.Get<LiteralPacket>();
                    try
                    {
                        text.Initialize($"Request timeout ({timeout}ms).");
                        _ = await context.Connection.Tcp.SendAsync(text.Serialize());

                        return;
                    }
                    finally
                    {
                        ObjectPoolManager.Instance.Return<LiteralPacket>(text);
                    }
                }

                // Await the execTaskAsTask only once
                System.Object? result = await execTaskAsTask;

                IReturnHandler<TPacket> returnHandler = ReturnTypeHandlerFactory<TPacket>.GetHandler(descriptor.ReturnType);
                await returnHandler.HandleAsync(result, context);
            }
            else
            {
                // Await the ValueTask only once
                System.Object? result = await execTask;

                IReturnHandler<TPacket> returnHandler = ReturnTypeHandlerFactory<TPacket>.GetHandler(descriptor.ReturnType);
                await returnHandler.HandleAsync(result, context);
            }
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

        LiteralPacket text = ObjectPoolManager.Instance.Get<LiteralPacket>();
        try
        {
            text.Initialize("Internal server error");
            _ = await context.Connection.Tcp.SendAsync(text.Serialize());

            return;
        }
        finally
        {
            ObjectPoolManager.Instance.Return<LiteralPacket>(text);
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static T EnsureNotNull<T>(T value, System.String paramName) where T : class
        => value ?? throw new System.ArgumentNullException(paramName);

    #endregion Private Methods
}
