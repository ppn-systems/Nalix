// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Packets.Abstractions;
using Nalix.Common.Protocols;
using Nalix.Network.Abstractions;
using Nalix.Network.Connection;
using Nalix.Network.Dispatch;

namespace Nalix.Network.Middleware.Inbound;

/// <summary>
/// Middleware that enforces a timeout for packet processing. If the next middleware or handler does not complete within the specified timeout,
/// a timeout response is sent to the client.
/// </summary>
public sealed class TimeoutMiddleware : IPacketMiddleware<IPacket>
{
    /// <inheritdoc/>
    public async System.Threading.Tasks.Task InvokeAsync(
        PacketContext<IPacket> context,
        System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task> next)
    {
        System.Int32 timeout = context.Attributes.Timeout?.TimeoutMilliseconds ?? 0;

        if (timeout > 0)
        {
            using System.Threading.CancellationTokenSource cts = new();
            using System.Threading.CancellationTokenSource execCts = new();

            System.Threading.Tasks.Task execution = next(execCts.Token);
            System.Threading.Tasks.Task delay = System.Threading.Tasks.Task.Delay(timeout, cts.Token);

            System.Threading.Tasks.Task completed = await System.Threading.Tasks.Task.WhenAny(execution, delay)
                                                                                     .ConfigureAwait(false);

            if (completed == delay)
            {
                execCts.Cancel();

                await context.Connection.SendAsync(
                    ControlType.TIMEOUT,
                    ProtocolCode.TIMEOUT,
                    ProtocolAction.RETRY,
                    sequenceId: (context.Packet as IPacketSequenced)?.SequenceId ?? 0,
                    flags: ControlFlags.IS_TRANSIENT,
                    // encode as steps of 100ms
                    arg0: (System.UInt32)(timeout / 100), arg1: 0, arg2: 0).ConfigureAwait(false);

                return;
            }

            cts.Cancel();
            await execution.ConfigureAwait(false);

            return;
        }

        await next(context.CancellationToken).ConfigureAwait(false);
    }
}
