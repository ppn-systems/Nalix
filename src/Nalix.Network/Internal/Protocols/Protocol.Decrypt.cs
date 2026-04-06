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
using Nalix.Network.Protocols;
using Nalix.Network.Connections;


#if DEBUG
using Microsoft.Extensions.Logging;
using Nalix.Framework.Injection;
#endif

namespace Nalix.Network.Internal.Protocols;

/// <summary>
/// Decrypts inbound frames when <see cref="PacketFlags.ENCRYPTED"/> is set.
/// </summary>
[DebuggerDisplay("Accepting={IsAccepting}, KeepConnectionOpen={KeepConnectionOpen}")]
internal sealed class ProtocolDecrypt : Protocol
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProtocolDecrypt"/> class.
    /// </summary>
    public ProtocolDecrypt()
    {
        this.IsAccepting = true;
        this.KeepConnectionOpen = true;
    }

    /// <inheritdoc />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override void ProcessMessage(object? sender, IConnectEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        // Must be able to replace lease.
        if (args is not ConnectionEventArgs replaceable)
        {
            return;
        }

        IBufferLease? lease = args.Lease;
        if (lease is null)
        {
            return;
        }

        if ((uint)lease.Length <= (int)PacketHeaderOffset.Flags)
        {
            return;
        }

        PacketFlags flags = lease.Span.ReadFlagsLE();

#if DEBUG
        string debugId = $"{args.Connection.NetworkEndpoint}/{args.Connection.ID}/leasePtr=0x{lease.GetHashCode():X8}";
        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Trace($"[DECRYPT][{debugId}] Start - Flags={flags} LeaseLen={lease.Length}");
#endif

        if (!flags.HasFlag(PacketFlags.ENCRYPTED))
        {
#if DEBUG
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Trace($"[DECRYPT][{debugId}] Bypass decryption (flags do not match). LeaseLen={lease.Length}");
#endif
            return;
        }

        if (args.Connection.Secret is not { } secret)
        {
            // Encrypted flag but no secret => reject/bypass (your middleware returned null).
            // Here we choose to disconnect as it is a protocol violation.
            args.Connection.Disconnect("Encrypted frame received before session key establishment.");
            return;
        }

        BufferLease dest;
        try
        {
            dest = BufferLease.Rent(FrameTransformer.GetPlaintextLength(lease.Span));
        }
        catch
        {
#if DEBUG
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[DECRYPT][{debugId}] Failed to get plaintext length.");
#endif
            return;
        }

        try
        {
            FrameTransformer.Decrypt(lease, dest, secret);
            dest.Span.WriteFlagsLE(flags.RemoveFlag(PacketFlags.ENCRYPTED));

            // Replace lease and dispose the old one.
            IBufferLease? old = replaceable.ReplaceLease(dest);
            old?.Dispose();

#if DEBUG
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Trace($"[DECRYPT][{debugId}] Decryption success! FlagsAfter={dest.Span.ReadFlagsLE()} DestLen={dest.Length}");
#endif
        }
        catch
        {
            dest.Dispose();
            throw;
        }
    }
}
