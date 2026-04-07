// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Diagnostics;
using Nalix.Common.Abstractions;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames;
using Nalix.Framework.Extensions;
using Nalix.Framework.Memory.Buffers;
using Nalix.Network.Connections;


#if DEBUG
using Microsoft.Extensions.Logging;
using Nalix.Framework.Injection;
#endif

namespace Nalix.Network.Internal.Protocols;

/// <summary>
/// Decompresses inbound frames when <see cref="PacketFlags.COMPRESSED"/> is set.
/// </summary>
[DebuggerDisplay("Accepting={IsAccepting}, KeepConnectionOpen={KeepConnectionOpen}")]
internal sealed class ProtocolDecompress : IProtocolStage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProtocolDecompress"/> class.
    /// </summary>
    public ProtocolDecompress()
    {
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void ProcessMessage(object? sender, IConnectEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        IBufferLease? lease = args.Lease;

        if (args is not ConnectionEventArgs replaceable)
        {
            throw new InvalidCastException("IConnectEventArgs must be ConnectionEventArgs.");
        }

        if (lease is null)
        {
            throw new InvalidOperationException("Event args must have Lease.");
        }

        if ((uint)lease.Length <= (int)PacketHeaderOffset.Flags)
        {
            throw new InvalidOperationException("Buffer length is invalid for decompression.");
        }

        PacketFlags flags = lease.Span.ReadFlagsLE();

#if DEBUG
        string debugId = $"{args.Connection.NetworkEndpoint}/{args.Connection.ID}/leasePtr=0x{lease.GetHashCode():X8}";
        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Trace($"[DECOMPRESS][{debugId}] Start - Flags={flags} LeaseLen={lease.Length}");
#endif

        if (!flags.HasFlag(PacketFlags.COMPRESSED))
        {
#if DEBUG
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Trace($"[DECOMPRESS][{debugId}] Bypass decompress (flags do not match). LeaseLen={lease.Length}");
#endif
            return;
        }

        BufferLease dest = BufferLease.Rent(
            FrameTransformer.GetDecompressedLength(lease.Span[FrameTransformer.Offset..]));

        try
        {
            FrameTransformer.Decompress(lease, dest);
            dest.Span.WriteFlagsLE(flags.RemoveFlag(PacketFlags.COMPRESSED));

            IBufferLease? old = replaceable.ReplaceLease(dest);
            old?.Dispose();

#if DEBUG
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Trace($"[DECOMPRESS][{debugId}] Decompression success! FlagsAfter={dest.Span.ReadFlagsLE()} DestLen={dest.Length}");
#endif
        }
        catch
        {
            dest.Dispose();
            throw;
        }
    }
}
