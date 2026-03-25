// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Nalix.Common.Diagnostics;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.Injection;

namespace Nalix.Network.Routing.Results.Memory;

/// <inheritdoc/>
[EditorBrowsable(EditorBrowsableState.Never)]
internal sealed class MemoryReturnHandler<TPacket> : IReturnHandler<TPacket> where TPacket : IPacket
{
    /// <inheritdoc/>
    public async ValueTask HandleAsync(
        [AllowNull] object result,
        PacketContext<TPacket> context)
    {
        if (result is not Memory<byte> memory)
        {
            return;
        }

        if (context?.Connection?.TCP == null)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Warn($"[NW.{nameof(MemoryReturnHandler<>)}:{nameof(HandleAsync)}] send-failed null");
            return;
        }

        try
        {
            bool sent = await context.Connection.TCP.SendAsync(memory).ConfigureAwait(false);
            if (!sent)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Warn($"[NW.{nameof(MemoryReturnHandler<>)}:{nameof(HandleAsync)}] send-failed");
            }
        }
        catch (Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[NW.{nameof(MemoryReturnHandler<>)}:{nameof(HandleAsync)}] error-serializing", ex);
        }
    }
}
