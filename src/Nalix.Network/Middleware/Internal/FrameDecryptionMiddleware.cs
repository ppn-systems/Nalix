// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Diagnostics;
using Nalix.Common.Middleware.Attributes;
using Nalix.Common.Networking.Abstractions;
using Nalix.Common.Networking.Packets.Enums;
using Nalix.Common.Shared.Caching;
using Nalix.Framework.Injection;
using Nalix.Shared.Extensions;
using Nalix.Shared.Frames;
using Nalix.Shared.Memory.Buffers;

namespace Nalix.Network.Middleware.Internal;

[MiddlewareOrder(-50)]
internal class FrameDecryptionMiddleware : INetworkBufferMiddleware
{
    public async System.Threading.Tasks.Task<IBufferLease> InvokeAsync(
        IBufferLease lease, IConnection connection, System.Threading.CancellationToken ct,
        System.Func<IBufferLease, System.Threading.CancellationToken, System.Threading.Tasks.Task<IBufferLease>> next)
    {
        if (lease.Span.ReadFlagsLE().HasFlag(PacketFlags.ENCRYPTED))
        {
            BufferLease dest;
            try
            {
                dest = BufferLease.Rent(FrameTransformer.GetPlaintextLength(lease.Span));
            }
            catch
            {
                return null;
            }

            if (!FrameTransformer.TryDecrypt(lease, dest, connection.Secret))
            {
                dest.Dispose();
#if DEBUG
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Debug($"Failed to decrypt frame from connection {connection.RemoteEndPoint}. Closing connection.");
#endif
                return null; // fallback if failed
            }

            dest.Span.WriteFlagsLE(lease.Span
                     .ReadFlagsLE()
                     .RemoveFlag(PacketFlags.ENCRYPTED));

            return await next(dest, ct).ConfigureAwait(false);
        }
        else
        {
            return await next(lease, ct).ConfigureAwait(false);
        }
    }
}