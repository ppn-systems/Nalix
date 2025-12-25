// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Diagnostics;
using Nalix.Common.Middleware;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Shared;
using Nalix.Framework.Injection;
using Nalix.Shared.Extensions;
using Nalix.Shared.Frames;
using Nalix.Shared.Memory.Buffers;

namespace Nalix.Network.Middleware.Internal;

/// <summary>
/// Middleware that decompresses the buffer payload using LZ4 or similar algorithms.
/// </summary>
[MiddlewareOrder(50)]
internal class FrameDecompressMiddleware : INetworkBufferMiddleware
{
    /// <inheritdoc />
    public async Task<IBufferLease> InvokeAsync(
        IBufferLease lease, IConnection connection,
        Func<IBufferLease, CancellationToken, Task<IBufferLease>> next,
        CancellationToken ct)
    {
#if DEBUG
        string debugId = $"{connection?.NetworkEndpoint}/{connection?.ID.ToString() ?? "?"}/leasePtr=0x{lease.GetHashCode():X8}";
        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Trace($"[DECOMPRESS][{debugId}] Start - Flags={lease.Span.ReadFlagsLE()} LeaseLen={lease.Length}");
#endif

        if (lease.Span.ReadFlagsLE().HasFlag(PacketFlags.COMPRESSED))
        {
            BufferLease dest = BufferLease.Rent(FrameTransformer.GetDecompressedLength(lease.Span[FrameTransformer.Offset..]));

#if DEBUG
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Trace($"[DECOMPRESS][{debugId}] Alloc decompress lease: DecompressLen={dest.Length}");
#endif

            if (!FrameTransformer.TryDecompress(lease, dest))
            {
                dest.Dispose();
#if DEBUG
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Debug($"[DECOMPRESS][{debugId}] Failed to decompress buffer! Flags={lease.Span.ReadFlagsLE()}");
#endif
                return null;
            }

            dest.Span.WriteFlagsLE(dest.Span
                 .ReadFlagsLE()
                 .RemoveFlag(PacketFlags.COMPRESSED));

#if DEBUG
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Trace($"[DECOMPRESS][{debugId}] Decompression success! FlagsAfter={dest.Span.ReadFlagsLE()} DestLen={dest.Length}");

            int sampleLen = Math.Min(16, dest.Length);
            string hexSample = BitConverter.ToString(dest.Span[..sampleLen].ToArray());
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Trace($"[DECOMPRESS][{debugId}] Decompressed buffer sample: {hexSample}");
#endif

            return await next(dest, ct).ConfigureAwait(false);
        }
        else
        {
#if DEBUG
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Trace($"[DECOMPRESS][{debugId}] Bypass decompress (flags do not match). LeaseLen={lease.Length}");
#endif

            return await next(lease, ct).ConfigureAwait(false);
        }
    }
}
