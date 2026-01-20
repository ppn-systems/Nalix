// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Nalix.Common.Diagnostics;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.Injection;

namespace Nalix.Network.Routing.Results.Packet;

/// <inheritdoc/>
[EditorBrowsable(EditorBrowsableState.Never)]
internal sealed class PacketReturnHandler<TPacket> : IReturnHandler<TPacket> where TPacket : IPacket
{
    /// <inheritdoc/>
    public async ValueTask HandleAsync(object? result, PacketContext<TPacket> context)
    {
        if (result is not TPacket packet)
        {
            return;
        }


        if (context.Sender == null)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Trace($"[NW.{nameof(PacketReturnHandler<>)}:{nameof(HandleAsync)}] send-failed transport=null");
            return;
        }

        try
        {
            bool sent = await context.Sender.SendAsync(packet).ConfigureAwait(false);

            if (!sent)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Trace($"[NW.{nameof(PacketReturnHandler<>)}:{nameof(HandleAsync)}] send-failed");
            }
        }
        catch (Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[NW.{nameof(PacketReturnHandler<>)}:{nameof(HandleAsync)}] error-serializing", ex);
        }
    }
}
