// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Abstractions;
using Nalix.Common.Middleware;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames;
using Nalix.Framework.Extensions;
using Nalix.Framework.Memory.Buffers;
using Microsoft.Extensions.Logging;


#if DEBUG
using Nalix.Framework.Injection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Nalix.Framework.Tests.")]
[assembly: InternalsVisibleTo("Nalix.Framework.Benchmarks")]
#endif

namespace Nalix.Network.Middleware.Internal;

/// <summary>
/// Middleware that decompresses the buffer payload using LZ4 or similar algorithms.
/// </summary>
[MiddlewareOrder(50)]
internal class FrameDecompressMiddleware : INetworkBufferMiddleware
{
    private static readonly ILogger? s_logger = InstanceManager.Instance.GetExistingInstance<ILogger>();

    /// <inheritdoc />
    public async Task<IBufferLease?> InvokeAsync(
        IBufferLease lease, IConnection connection,
        Func<IBufferLease, CancellationToken, Task<IBufferLease?>> next, CancellationToken ct)
    {
#if DEBUG
        string debugId = $"{connection?.NetworkEndpoint}/{connection?.ID.ToString() ?? "?"}/leasePtr=0x{lease.GetHashCode():X8}";
        if (s_logger?.IsEnabled(LogLevel.Trace) == true)
        {
            s_logger.LogTrace(
                "[DECOMPRESS][{DebugId}] Start - Flags={Flags} LeaseLen={LeaseLen}",
                debugId,
                lease.Span.ReadFlagsLE(),
                lease.Length);
        }
#endif

        if (lease.Span.ReadFlagsLE().HasFlag(PacketFlags.COMPRESSED))
        {
            BufferLease dest = BufferLease.Rent(FrameTransformer.GetDecompressedLength(lease.Span[FrameTransformer.Offset..]));

#if DEBUG
            if (s_logger?.IsEnabled(LogLevel.Trace) == true)
            {
                s_logger.LogTrace(
                    "[DECOMPRESS][{DebugId}] Alloc decompress lease: DecompressLen={DecompressLen}",
                    debugId,
                    dest.Length);
            }
#endif

            try
            {
                FrameTransformer.Decompress(lease, dest);

                dest.Span.WriteFlagsLE(dest.Span
                     .ReadFlagsLE()
                     .RemoveFlag(PacketFlags.COMPRESSED));

#if DEBUG
                if (s_logger?.IsEnabled(LogLevel.Trace) == true)
                {
                    s_logger.LogTrace(
                        "[DECOMPRESS][{DebugId}] Decompression success! FlagsAfter={FlagsAfter} DestLen={DestLen}",
                        debugId,
                        dest.Span.ReadFlagsLE(),
                        dest.Length);
                }

                int sampleLen = Math.Min(16, dest.Length);
                string hexSample = BitConverter.ToString(dest.Span[..sampleLen].ToArray());

                if (s_logger?.IsEnabled(LogLevel.Trace) == true)
                {
                    s_logger.LogTrace(
                        "[DECOMPRESS][{DebugId}] Decompressed buffer sample: {HexSample}",
                        debugId,
                        hexSample);
                }
#endif

                return await next(dest, ct).ConfigureAwait(false);
            }
            catch
            {
                dest.Dispose();
                throw;
            }
        }
        else
        {
#if DEBUG
            if (s_logger?.IsEnabled(LogLevel.Trace) == true)
            {
                s_logger.LogTrace(
                    "[DECOMPRESS][{DebugId}] Bypass decompress (flags do not match). LeaseLen={LeaseLen}",
                    debugId,
                    lease.Length);
            }
#endif

            return await next(lease, ct).ConfigureAwait(false);
        }
    }
}
