using Nalix.Common.Packets.Interfaces;
using Nalix.Network.Dispatch.Core;
using Nalix.Network.Dispatch.Middleware.Core;
using Nalix.Network.Dispatch.Middleware.Enums;
using Nalix.Network.Dispatch.Middleware.Interfaces;
using Nalix.Shared.Memory.Pooling;
using Nalix.Shared.Transport;

namespace Nalix.Network.Dispatch.Middleware.PreDispatch;

/// <summary>
/// Middleware that enforces a timeout for packet processing. If the next middleware or handler does not complete within the specified timeout,
/// a timeout response is sent to the client.
/// </summary>
/// <typeparam name="TPacket">The packet type implementing <see cref="IPacket"/> and <see cref="IPacketTransformer{TPacket}"/>.</typeparam>
[PacketMiddleware(MiddlewareStage.PreDispatch, order: 1, name: "Timeout")]
public sealed class TimeoutMiddleware<TPacket> : IPacketMiddleware<TPacket>
    where TPacket : IPacket, IPacketTransformer<TPacket>
{
    /// <inheritdoc/>
    public async System.Threading.Tasks.Task InvokeAsync(
        PacketContext<TPacket> context,
        System.Func<System.Threading.Tasks.Task> next)
    {
        System.Int32 timeout = context.Attributes.Timeout?.TimeoutMilliseconds ?? 0;

        if (timeout > 0)
        {
            System.Threading.Tasks.Task execution = next();
            System.Threading.Tasks.Task delay = System.Threading.Tasks.Task.Delay(timeout);

            System.Threading.Tasks.Task completed = await System.Threading.Tasks.Task.WhenAny(execution, delay);

            if (completed == delay)
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
                    ObjectPoolManager.Instance.Return(text);
                }
            }

            await execution;
        }
        else
        {
            await next();
        }
    }
}
