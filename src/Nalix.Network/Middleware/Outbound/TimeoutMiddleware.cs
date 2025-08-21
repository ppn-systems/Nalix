// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Packets.Abstractions;
using Nalix.Common.Packets.Attributes;
using Nalix.Common.Packets.Enums;
using Nalix.Network.Abstractions;
using Nalix.Network.Dispatch;

namespace Nalix.Network.Middleware.Outbound;

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

            System.Threading.Tasks.Task completed = await System.Threading.Tasks.Task.WhenAny(execution, delay)
                                                                                     .ConfigureAwait(false);

            if (completed == delay)
            {
                _ = await context.Connection.Tcp.SendAsync($"Request timeout ({timeout}ms).")
                                                .ConfigureAwait(false);
            }
            else
            {
                cts.Cancel();
                await execution.ConfigureAwait(false);
            }
        }
        else
        {
            await next();
        }
    }
}
