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
internal class DecryptMiddleware : INetworkBufferMiddleware
{
    public ValueTask<IBufferLease?> InvokeAsync(IBufferLease lease, IConnection connection, CancellationToken ct)
    {
        if (lease is null || connection is null)
        {
            return ValueTask.FromResult<IBufferLease?>(null);
        }

        IConnection safeConnection = connection;

#if DEBUG
        string debugId = $"{safeConnection.NetworkEndpoint}/{safeConnection.ID}/leasePtr=0x{lease.GetHashCode():X8}";
        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Trace($"[DECRYPT][{debugId}] Start - Flags={lease.Span.ReadFlagsLE()} LeaseLen={lease.Length}");
#endif

        if ((uint)lease.Length <= (int)PacketHeaderOffset.Flags)
        {
            return ValueTask.FromResult<IBufferLease?>(null);
        }

        PacketFlags flags = lease.Span.ReadFlagsLE();
        if (flags.HasFlag(PacketFlags.ENCRYPTED))
        {
            if (safeConnection.Secret is not { } secret)
            {
                return ValueTask.FromResult<IBufferLease?>(null);
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
                return ValueTask.FromResult<IBufferLease?>(null);
            }

            try
            {
                FrameTransformer.Decrypt(lease, dest, secret);

                dest.Span.WriteFlagsLE(flags.RemoveFlag(PacketFlags.ENCRYPTED));

#if DEBUG
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Trace($"[DECRYPT][{debugId}] Decryption success! FlagsAfter={dest.Span.ReadFlagsLE()} DestLen={dest.Length}");

                int sampleLen = Math.Min(16, dest.Length);
                string hexSample = BitConverter.ToString(dest.Span[..sampleLen].ToArray());
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Trace($"[DECRYPT][{debugId}] Decrypted buffer sample: {hexSample}");
#endif

                return ValueTask.FromResult<IBufferLease?>(dest);
            }
            catch
            {
                dest.Dispose();
                throw;
            }
        }

#if DEBUG
        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Trace($"[DECRYPT][{debugId}] Bypass decryption (flags do not match). LeaseLen={lease.Length}");
#endif
        return ValueTask.FromResult<IBufferLease?>(lease);
    }
}
