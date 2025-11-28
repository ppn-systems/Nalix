// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Middleware.Attributes;
using Nalix.Common.Networking.Abstractions;
using Nalix.Common.Networking.Packets.Enums;
using Nalix.Common.Shared.Caching;
using Nalix.Network.Abstractions;
using Nalix.Shared.Extensions;
using Nalix.Shared.Frames;
using Nalix.Shared.Memory.Buffers;

namespace Nalix.Network.Middleware.Internal;

/// <summary>
/// Middleware that compresses the buffer payload using LZ4 or similar algorithms.
/// </summary>
[MiddlewareOrder(50)]
internal class FrameDecompressMiddleware : INetworkBufferMiddleware
{
    /// <inheritdoc />
    public async System.Threading.Tasks.Task<IBufferLease> InvokeAsync(
        IBufferLease lease, IConnection connection, System.Threading.CancellationToken ct,
        System.Func<IBufferLease, System.Threading.CancellationToken, System.Threading.Tasks.Task<IBufferLease>> next)
    {
        if (lease.Span.ReadFlagsLE().HasFlag(PacketFlags.ENCRYPTED))
        {

            BufferLease dest = BufferLease.Rent(FrameTransformer
                                          .GetDecompressedLength(lease.Span[FrameTransformer.Offset..]));

            if (!FrameTransformer.TryDecompress(lease, dest))
            {
                dest.Dispose();
                return null;
            }

            dest.Span.WriteFlagsLE(dest.Span
                     .ReadFlagsLE()
                     .RemoveFlag(PacketFlags.COMPRESSED));

            return await next(dest, ct).ConfigureAwait(false);
        }
        else
        {
            return await next(lease, ct).ConfigureAwait(false);
        }
    }
}