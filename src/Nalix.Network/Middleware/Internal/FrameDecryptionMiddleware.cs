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

[MiddlewareOrder(-50)]
internal class FrameDecryptionMiddleware : INetworkBufferMiddleware
{
    private static readonly ILogger? s_logger = InstanceManager.Instance.GetExistingInstance<ILogger>();

    public async Task<IBufferLease?> InvokeAsync(
        IBufferLease lease, IConnection connection,
        Func<IBufferLease, CancellationToken, Task<IBufferLease?>> next, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(next);

        IConnection safeConnection = connection;

#if DEBUG
        string debugId = $"{safeConnection.NetworkEndpoint}/{safeConnection.ID}/leasePtr=0x{lease.GetHashCode():X8}";
        if (s_logger?.IsEnabled(LogLevel.Trace) == true)
        {
            s_logger.LogTrace(
                "[DECRYPT][{DebugId}] Start - Flags={Flags} LeaseLen={LeaseLen}",
                debugId,
                lease.Span.ReadFlagsLE(),
                lease.Length);
        }
#endif

        if (lease.Span.ReadFlagsLE().HasFlag(PacketFlags.ENCRYPTED))
        {
            if (safeConnection.Secret is not { } secret)
            {
                return null;
            }

            BufferLease dest;
            try
            {
#if DEBUG
                if (s_logger?.IsEnabled(LogLevel.Trace) == true)
                {
                    s_logger.LogTrace(
                        "[DECRYPT][{DebugId}] Alloc decrypt lease: PlaintextLen={PlaintextLen}",
                        debugId,
                        FrameTransformer.GetPlaintextLength(lease.Span));
                }
#endif
                dest = BufferLease.Rent(FrameTransformer.GetPlaintextLength(lease.Span));
            }
            catch
            {
#if DEBUG
                if (s_logger?.IsEnabled(LogLevel.Error) == true)
                {
                    s_logger.LogError(
                        "[DECRYPT][{DebugId}] Failed to get plaintext length.",
                        debugId);
                }
#endif
                return null;
            }

            try
            {
                FrameTransformer.Decrypt(lease, dest, secret);

                dest.Span.WriteFlagsLE(lease.Span
                         .ReadFlagsLE()
                         .RemoveFlag(PacketFlags.ENCRYPTED));

#if DEBUG
                if (s_logger?.IsEnabled(LogLevel.Trace) == true)
                {
                    s_logger.LogTrace(
                        "[DECRYPT][{DebugId}] Decryption success! FlagsAfter={FlagsAfter} DestLen={DestLen}",
                        debugId,
                        dest.Span.ReadFlagsLE(),
                        dest.Length);
                }

                int sampleLen = Math.Min(16, dest.Length);
                string hexSample = BitConverter.ToString(dest.Span[..sampleLen].ToArray());

                if (s_logger?.IsEnabled(LogLevel.Trace) == true)
                {
                    s_logger.LogTrace(
                        "[DECRYPT][{DebugId}] Decrypted buffer sample: {HexSample}",
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
                    "[DECRYPT][{DebugId}] Bypass decryption (flags do not match). LeaseLen={LeaseLen}",
                    debugId,
                    lease.Length);
            }
#endif

            return await next(lease, ct).ConfigureAwait(false);
        }
    }
}
