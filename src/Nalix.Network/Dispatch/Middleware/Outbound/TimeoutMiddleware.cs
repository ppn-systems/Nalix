// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Packets.Interfaces;
using Nalix.Network.Dispatch.Core.Context;
using Nalix.Network.Dispatch.Middleware.Core.Attributes;
using Nalix.Network.Dispatch.Middleware.Core.Enums;
using Nalix.Network.Dispatch.Middleware.Core.Interfaces;
using Nalix.Shared.Memory.Pooling;
using Nalix.Shared.Messaging.Text;

namespace Nalix.Network.Dispatch.Middleware.Outbound;

/// <summary>
/// Middleware that enforces a timeout for packet processing. If the next middleware or handler does not complete within the specified timeout,
/// a timeout response is sent to the client.
/// </summary>
[PacketMiddleware(MiddlewareStage.PreDispatch, order: 1, name: "Timeout")]
public sealed class TimeoutMiddleware : IPacketMiddleware<IPacket>
{
    /// <inheritdoc/>
    public async System.Threading.Tasks.Task InvokeAsync(
        PacketContext<IPacket> context,
        System.Func<System.Threading.Tasks.Task> next)
    {
        System.Int32 timeout = context.Attributes.Timeout?.TimeoutMilliseconds ?? 0;

        if (timeout > 0)
        {
            System.Threading.Tasks.Task execution = next();
            using System.Threading.CancellationTokenSource cts = new();
            System.Threading.Tasks.Task delay = System.Threading.Tasks.Task.Delay(timeout, cts.Token);

            System.Threading.Tasks.Task completed = await System.Threading.Tasks.Task.WhenAny(execution, delay);

            if (completed == delay)
            {
                Text256 text = ObjectPoolManager.Instance.Get<Text256>();
                try
                {
                    text.Initialize($"Request timeout ({timeout}ms).");
                    _ = await context.Connection.Tcp.SendAsync(text.Serialize());

                    return;
                }
                finally
                {
                    ObjectPoolManager.Instance.Return(text);
                }
            }
            else
            {
                cts.Cancel();
                await execution;
            }
        }
        else
        {
            await next();
        }
    }
}
