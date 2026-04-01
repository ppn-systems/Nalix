// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nalix.Common.Abstractions;
using Nalix.Common.Middleware;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames;
using Nalix.Framework.Extensions;
using Nalix.Framework.Memory.Buffers;

#if DEBUG
using Nalix.Framework.Injection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Nalix.Framework.Tests.")]
[assembly: InternalsVisibleTo("Nalix.Framework.Benchmarks")]
#endif

namespace Nalix.Network.Middleware.Internal;

/// <summary>
/// Middleware that decompresses packet payloads when <see cref="PacketFlags.COMPRESSED"/> is set.
/// </summary>
[MiddlewareOrder(50)]
internal class DecompressMiddleware : INetworkBufferMiddleware
{
    /// <inheritdoc />
    public ValueTask<IBufferLease?> InvokeAsync(IBufferLease lease, IConnection connection, CancellationToken ct)
    {
        if (lease is null || connection is null)
        {
            return ValueTask.FromResult<IBufferLease?>(null);
        }

        if ((uint)lease.Length <= (int)PacketHeaderOffset.Flags)
        {
            return ValueTask.FromResult<IBufferLease?>(null);
        }

        PacketFlags flags = lease.Span.ReadFlagsLE();

#if DEBUG
        string debugId = $"{connection.NetworkEndpoint}/{connection.ID}/leasePtr=0x{lease.GetHashCode():X8}";
        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Trace($"[DECOMPRESS][{debugId}] Start - Flags={flags} LeaseLen={lease.Length}");
#endif

        if (!flags.HasFlag(PacketFlags.COMPRESSED))
        {
#if DEBUG
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Trace($"[DECOMPRESS][{debugId}] Bypass decompress (flags do not match). LeaseLen={lease.Length}");
#endif
            return ValueTask.FromResult<IBufferLease?>(lease);
        }

        BufferLease dest = BufferLease.Rent(FrameTransformer.GetDecompressedLength(lease.Span[FrameTransformer.Offset..]));

#if DEBUG
        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Trace($"[DECOMPRESS][{debugId}] Alloc decompress lease: DecompressLen={dest.Length}");
#endif

        try
        {
            FrameTransformer.Decompress(lease, dest);
            dest.Span.WriteFlagsLE(flags.RemoveFlag(PacketFlags.COMPRESSED));

#if DEBUG
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Trace($"[DECOMPRESS][{debugId}] Decompression success! FlagsAfter={dest.Span.ReadFlagsLE()} DestLen={dest.Length}");

            int sampleLen = Math.Min(16, dest.Length);
            string hexSample = BitConverter.ToString(dest.Span[..sampleLen].ToArray());
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Trace($"[DECOMPRESS][{debugId}] Decompressed buffer sample: {hexSample}");
#endif

            return ValueTask.FromResult<IBufferLease?>(dest);
        }
        catch
        {
            dest.Dispose();
            throw;
        }
    }
}
