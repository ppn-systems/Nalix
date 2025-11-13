// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Nalix.Common.Diagnostics;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.Injection;

namespace Nalix.Network.Routing.Results.Primitives;

/// <inheritdoc/>
[EditorBrowsable(EditorBrowsableState.Never)]
internal sealed class ByteArrayReturnHandler<TPacket> : IReturnHandler<TPacket> where TPacket : IPacket
{
    /// <inheritdoc/>
    public async ValueTask HandleAsync(
        object? result,
        PacketContext<TPacket> context)
    {
        if (result is not byte[] data)
        {
            return;
        }

        if (data.Length == 0)
        {
            return;
        }

        if (context?.Connection?.TCP == null)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Warn($"[NW.{nameof(ByteArrayReturnHandler<>)}:{nameof(HandleAsync)}] send-failed transport=null");
            return;
        }

        try
        {
            bool sent = await context.Connection.TCP.SendAsync(data).ConfigureAwait(false);
            if (!sent)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Warn($"[NW.{nameof(ByteArrayReturnHandler<>)}:{nameof(HandleAsync)}] send-failed");
            }
        }
        catch (Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[NW.{nameof(ByteArrayReturnHandler<>)}:{nameof(HandleAsync)}] error-serializing", ex);
        }
    }
}
