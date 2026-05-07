// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using Nalix.Abstractions;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Primitives;
using Nalix.Abstractions.Security;
using Nalix.Codec.Extensions;
using Nalix.Codec.Internal;
using Nalix.Codec.Memory;
using Nalix.Codec.Security;

namespace Nalix.Codec.Transforms;

/// <summary>
/// Unifies the execution of cryptographic and compression transforms for inbound and outbound frames.
/// </summary>
public static class FramePipeline
{
    /// <summary>
    /// Applies inbound transforms in transport order: decrypt first, then decompress.
    /// Mutates the <paramref name="current"/> lease directly via <see langword="ref"/> to optimize performance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ProcessInbound([Borrowed] ref IBufferLease current, ReadOnlySpan<byte> secret, CipherSuiteType algorithm)
    {
        ArgumentNullException.ThrowIfNull(current);

        IBufferLease original = current;
        PacketFlags flags = current.Span.AsHeaderRef().Flags;

        if ((flags & PacketFlags.ENCRYPTED) != 0)
        {
            if (algorithm == CipherSuiteType.None)
            {
                Throw.EncryptedButNoCipher();
            }

            if (secret.IsEmpty)
            {
                Throw.EncryptedButNoKey();
            }

            try
            {
                current = FrameCipher.DecryptFrame(current, secret, algorithm);

                // Re-read flags after decryption since the inner payload might have other flags (e.g., COMPRESSED).
                flags = current.Span.AsHeaderRef().Flags;
            }
            catch (Exception)
            {
                throw;
            }
        }

        if ((flags & PacketFlags.COMPRESSED) != 0)
        {
            IBufferLease prev = current;
            current = FrameCompression.DecompressFrame(current);

            // If we replaced a buffer that was ALREADY a replacement (intermediate),
            // we must dispose it to avoid a leak. We do NOT dispose the 'original' one.
            if (!ReferenceEquals(prev, original))
            {
                prev.Dispose();
            }
        }
    }

    /// <summary>
    /// Applies outbound transforms in transport order: compress first, then encrypt.
    /// Mutates the <paramref name="current"/> lease directly via <see langword="ref"/> to optimize performance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ProcessOutbound(
        [Borrowed] ref IBufferLease current, bool enableCompress,
        int minSizeToCompress, bool enableEncrypt, ReadOnlySpan<byte> secret, CipherSuiteType algorithm)
    {
        ArgumentNullException.ThrowIfNull(current);

        IBufferLease original = current;

        int payloadSize = current.Length - FrameTransformer.Offset;
        bool doCompress = enableCompress && payloadSize >= minSizeToCompress;

        if (enableEncrypt && algorithm == CipherSuiteType.None)
        {
            Throw.EncryptRequestedButNoCipher();
        }

        if (doCompress && enableEncrypt)
        {
            ProcessOutboundFused(ref current, payloadSize, secret, algorithm);
        }
        else if (doCompress)
        {
            current = FrameCompression.CompressFrame(current);
        }
        else if (enableEncrypt)
        {
            IBufferLease prev = current;
            current = FrameCipher.EncryptFrame(current, secret, algorithm);

            // If we replaced a buffer that was ALREADY a replacement (intermediate),
            // we must dispose it to avoid a leak. We do NOT dispose the 'original' one.
            if (!ReferenceEquals(prev, original))
            {
                prev.Dispose();
            }
        }
    }

    private static void ProcessOutboundFused([Borrowed] ref IBufferLease current, int payloadSize, ReadOnlySpan<byte> secret, CipherSuiteType algorithm)
    {
        // 1. Calculate maximum required sizes
        int maxCompSize = FrameTransformer.GetMaxCompressedSize(payloadSize);
        int maxFinalSize = FrameTransformer.GetMaxCiphertextSize(algorithm, maxCompSize);

        // 2. RENT A SINGLE LEASE: capacity = Header + Final Ciphertext + Temp Compressed Data
        int totalRequiredCapacity = FrameTransformer.Offset + maxFinalSize + maxCompSize;
        BufferLease singleLease = BufferLease.Rent(totalRequiredCapacity);

        try
        {
            ReadOnlySpan<byte> srcSpan = current.Span;
            Span<byte> destFull = singleLease.SpanFull;

            // 3. Slice regions from the same lease
            Span<byte> finalRegion = destFull.Slice(FrameTransformer.Offset, maxFinalSize);
            Span<byte> tempRegion = destFull.Slice(FrameTransformer.Offset + maxFinalSize, maxCompSize);

            // 4. LZ4 compress directly into tempRegion
            int compLen = LZ4.LZ4Codec.Encode(srcSpan[FrameTransformer.Offset..], tempRegion);

            // 5. Encrypt from tempRegion into finalRegion
            EnvelopeCipher.Encrypt(secret, tempRegion[..compLen], finalRegion, null, null, algorithm, out int encLen);

            // [SECURITY] 5.5: Clear intermediate compressed data from the memory pool
            tempRegion[..compLen].Clear();

            // 6. Copy header once and set flags
            srcSpan[..FrameTransformer.Offset].CopyTo(destFull[..FrameTransformer.Offset]);

            ref PacketHeader header = ref destFull.AsHeaderRef();
            header.Flags |= PacketFlags.COMPRESSED | PacketFlags.ENCRYPTED;

            // 7. Finalize length
            singleLease.CommitLength(FrameTransformer.Offset + encLen);

            // IMPORTANT: Only swap the reference.
            // DO NOT call current.Dispose() here to preserve the original lease ownership rule.
            current = singleLease;
        }
        catch (Exception ex) when (Abstractions.Exceptions.ExceptionClassifier.IsNonFatal(ex))
        {
            singleLease.Dispose();
            throw;
        }
    }
}
