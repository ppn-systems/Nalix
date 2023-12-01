// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Network.Dispatch.Delegates;
using Nalix.Network.Dispatch.Results;
using Nalix.Shared.Injection;
using Nalix.Shared.Memory.Pooling;
using Nalix.Shared.Messaging.Text;

namespace Nalix.Network.Dispatch.Options;

public sealed partial class PacketDispatchOptions<TPacket>
{
    #region Private Methods

    private async System.Threading.Tasks.ValueTask ExecuteHandler(
        PacketHandler<TPacket> descriptor,
        PacketContext<TPacket> context)
    {
        if (this._pipeline is not null)
        {
            await this._pipeline.ExecuteAsync(context, Terminal).ConfigureAwait(false);
        }
        else
        {
            await Terminal().ConfigureAwait(false);
        }

        async System.Threading.Tasks.Task Terminal()
        {
            try
            {
                // Execute the handler and await the ValueTask once
                System.Object? result = await descriptor.ExecuteAsync(context);

                // Handle the result
                IReturnHandler<TPacket> returnHandler = ReturnTypeHandlerFactory<TPacket>.GetHandler(descriptor.ReturnType);
                await returnHandler.HandleAsync(result, context);
            }
            catch (System.Exception ex)
            {
                await this.HandleExecutionException(descriptor, context, ex);
            }
        }
    }

    private async System.Threading.Tasks.ValueTask HandleExecutionException(
        PacketHandler<TPacket> descriptor,
        PacketContext<TPacket> context,
        System.Exception exception)
    {
        this.Logger?.Error("Handler execution failed for OpCode={0}: {1}",
            descriptor.OpCode, exception.Message);

        this._errorHandler?.Invoke(exception, descriptor.OpCode);

        Text256 text = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                               .Get<Text256>();
        try
        {
            text.Initialize("Internal server error");
            _ = await context.Connection.Tcp.SendAsync(text.Serialize());

            return;
        }
        finally
        {
            InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .Return<Text256>(text);
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static T EnsureNotNull<T>(T value, System.String paramName) where T : class
        => value ?? throw new System.ArgumentNullException(paramName);

    #endregion Private Methods
}
