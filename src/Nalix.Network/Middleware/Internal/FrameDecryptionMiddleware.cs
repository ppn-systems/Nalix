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

#if DEBUG
using Nalix.Common.Diagnostics;
using Nalix.Framework.Injection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Nalix.Framework.Tests.")]
[assembly: InternalsVisibleTo("Nalix.Framework.Benchmarks")]
#endif

namespace Nalix.Network.Middleware.Internal;

[MiddlewareOrder(-50)]
internal class FrameDecryptionMiddleware : INetworkBufferMiddleware
{
    public async Task<IBufferLease?> InvokeAsync(
        IBufferLease lease, IConnection connection,
        Func<IBufferLease, CancellationToken, Task<IBufferLease?>> next,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(next);

        IConnection safeConnection = connection;

#if DEBUG
        string debugId = $"{safeConnection.NetworkEndpoint}/{safeConnection.ID}/leasePtr=0x{lease.GetHashCode():X8}";
        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Trace($"[DECRYPT][{debugId}] Start - Flags={lease.Span.ReadFlagsLE()} LeaseLen={lease.Length}");
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
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Trace($"[DECRYPT][{debugId}] Alloc decrypt lease: PlaintextLen={FrameTransformer.GetPlaintextLength(lease.Span)}");
#endif      

                dest = BufferLease.Rent(FrameTransformer.GetPlaintextLength(lease.Span));
            }
            catch
            {
#if DEBUG
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Error($"[DECRYPT][{debugId}] Failed to get plaintext length.");
#endif
                return null;
            }

            if (!FrameTransformer.TryDecrypt(lease, dest, secret))
            {
                dest.Dispose();
#if DEBUG
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Debug($"[DECRYPT][{debugId}] Failed to decrypt frame! Flags={lease.Span.ReadFlagsLE()} - Closing connection.");
#endif
                return null; // fallback if failed
            }

            dest.Span.WriteFlagsLE(lease.Span
                     .ReadFlagsLE()
                     .RemoveFlag(PacketFlags.ENCRYPTED));

#if DEBUG
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Trace($"[DECRYPT][{debugId}] Decryption success! FlagsAfter={dest.Span.ReadFlagsLE()} DestLen={dest.Length}");

            int sampleLen = Math.Min(16, dest.Length);
            string hexSample = BitConverter.ToString(dest.Span[..sampleLen].ToArray());
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Trace($"[DECRYPT][{debugId}] Decrypted buffer sample: {hexSample}");
#endif

            return await next(dest, ct).ConfigureAwait(false);
        }
        else
        {
#if DEBUG
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Trace($"[DECRYPT][{debugId}] Bypass decryption (flags do not match). LeaseLen={lease.Length}");
#endif

            return await next(lease, ct).ConfigureAwait(false);
        }
    }
}
