// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Primitives;
using Nalix.Codec.Transforms;
using Nalix.Environment.Hashing;
using Nalix.Environment.Memory;

namespace Nalix.Runtime.Internal.Pipelines;

/// <summary>
/// Shared zero-allocation helpers for serializing and framing packets.
/// Used by both PacketSender (unicast) and ConnectionHubExtensions (broadcast/multicast).
/// </summary>
internal static class PacketPipeline
{
    /// <summary>
    /// Serializes a packet into a pooled BufferLease.
    /// The caller owns the returned lease and must dispose it.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static BufferLease Serialize(IPacket packet)
    {
        int packetLength = packet.Length;
        BufferLease lease = BufferLease.Rent(packetLength);
        int written = packet.Serialize(lease.SpanFull);
        lease.CommitLength(written);
        return lease;
    }

    /// <summary>
    /// Applies FramePipeline.ProcessOutbound + UDP signing (if applicable) per-connection,
    /// then sends via the specified transport.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static async ValueTask ProcessAndSendAsync(
        IConnection connection, IConnection.ITransport transport, [Borrowed] IBufferLease rawLease,
        bool needEncrypt, bool enableCompress, int minSizeToCompress, CancellationToken ct, bool cloneLease = true)
    {
        // Clone raw lease for per-connection mutation if cloneLease is true
        BufferLease workingLease = cloneLease ? BufferLease.CopyFrom(rawLease.Span) : (BufferLease)rawLease;

        try
        {
            IBufferLease current = workingLease;
            uint? sequenceToUse = needEncrypt ? transport.NextSendSequence() : null;

            // FramePipeline mutates `current` and properly cleans up older leases.
            FramePipeline.ProcessOutbound(
                ref current,
                enableCompress,
                minSizeToCompress,
                needEncrypt,
                connection.Secret.AsSpan(),
                sequenceToUse,
                connection.Algorithm);

            try
            {
                if (transport == connection.UDP)
                {
                    int dataLen = current.Length;
                    BufferLease signedLease = BufferLease.Rent(dataLen + Math.Max(4, Bytes32.Size));
                    try
                    {
                        current.Span.CopyTo(signedLease.SpanFull);
                        connection.Secret.AsSpan().CopyTo(signedLease.SpanFull[dataLen..]);
                        uint hash = XxHash32.Compute(signedLease.SpanFull[..(dataLen + Bytes32.Size)]);
                        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(signedLease.SpanFull.Slice(dataLen, 4), hash);
                        signedLease.CommitLength(dataLen + 4);

                        await transport.SendAsync(signedLease.Memory, ct).ConfigureAwait(false);
                    }
                    finally
                    {
                        signedLease.Dispose();
                    }
                }
                else
                {
                    await transport.SendAsync(current.Memory, ct).ConfigureAwait(false);
                }
            }
            finally
            {
                // Only dispose `current` if it was replaced. 
                // `workingLease` itself will be disposed in the outer finally or by the caller.
                if (current != workingLease)
                {
                    current.Dispose();
                }
            }
        }
        finally
        {
            if (cloneLease)
            {
                workingLease.Dispose();
            }
        }
    }
}
