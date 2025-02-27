// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Logging;
using Nalix.Framework.Injection;

namespace Nalix.Network.Dispatch.Results.Primitives;

/// <inheritdoc/>
internal sealed class ByteArrayReturnHandler<TPacket> : IReturnHandler<TPacket>
{
    /// <inheritdoc/>
    public async System.Threading.Tasks.ValueTask HandleAsync(
        [System.Diagnostics.CodeAnalysis.AllowNull] System.Object result,
        [System.Diagnostics.CodeAnalysis.NotNull] PacketContext<TPacket> context)
    {
        if (result is not System.Byte[] data)
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
                                    .Warn($"[NW.{nameof(ByteArrayReturnHandler<TPacket>)}:{nameof(HandleAsync)}] connection or TCP transport is null");
            return;
        }

        try
        {
            System.Boolean sent = await context.Connection.TCP.SendAsync(data).ConfigureAwait(false);
            if (!sent)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Warn($"[NW.{nameof(ByteArrayReturnHandler<TPacket>)}:{nameof(HandleAsync)}] send failed");
            }
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[NW.{nameof(ByteArrayReturnHandler<TPacket>)}:{nameof(HandleAsync)}] error sending byte array", ex);
        }
    }
}