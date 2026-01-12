// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Logging;
using Nalix.Framework.Injection;

namespace Nalix.Network.Dispatch.Results.Memory;

/// <inheritdoc/>
internal sealed class MemoryReturnHandler<TPacket> : IReturnHandler<TPacket>
{
    /// <inheritdoc/>
    public async System.Threading.Tasks.ValueTask HandleAsync(
        [System.Diagnostics.CodeAnalysis.AllowNull] System.Object result,
        [System.Diagnostics.CodeAnalysis.NotNull] PacketContext<TPacket> context)
    {
        if (result is not System.Memory<System.Byte> memory)
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
            System.Boolean sent = await context.Connection.TCP.SendAsync(memory).ConfigureAwait(false);
            if (!sent)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Warn($"[NW.{nameof(MemoryReturnHandler<>)}:{nameof(HandleAsync)}] send-failed");
            }
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[NW.{nameof(MemoryReturnHandler<>)}:{nameof(HandleAsync)}] error-serializing", ex);
        }
    }
}