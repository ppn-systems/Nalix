// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Diagnostics;
using Nalix.Common.Messaging.Packets.Abstractions;
using Nalix.Framework.Injection;

namespace Nalix.Network.Dispatch.Results.Packet;

/// <inheritdoc/>
internal sealed class PacketReturnHandler<TPacket> : IReturnHandler<TPacket>
    where TPacket : IPacket
{
    /// <inheritdoc/>
    public async System.Threading.Tasks.ValueTask HandleAsync(
        [System.Diagnostics.CodeAnalysis.AllowNull] System.Object result,
        [System.Diagnostics.CodeAnalysis.NotNull] PacketContext<TPacket> context)
    {
        if (result is not TPacket packet)
        {
            return;
        }

        if (context?.Connection?.TCP == null)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Warn($"[NW.{nameof(PacketReturnHandler<>)}:{nameof(HandleAsync)}] send-failed transport=null");
            return;
        }

        try
        {
            System.ReadOnlyMemory<System.Byte> bytes = packet.Serialize();
            System.Boolean sent = await context.Connection.TCP.SendAsync(bytes).ConfigureAwait(false);

            if (!sent)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Warn($"[NW.{nameof(PacketReturnHandler<>)}:{nameof(HandleAsync)}] send-failed");
            }
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[NW.{nameof(PacketReturnHandler<>)}:{nameof(HandleAsync)}] error-serializing", ex);
        }
    }
}