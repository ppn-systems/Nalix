// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using Nalix.Abstractions;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Primitives;
using Nalix.Abstractions.Security;
using Nalix.Codec.Internal;
using Nalix.Codec.Security;
using Nalix.Environment.Extensions;
using Nalix.Environment.Memory;

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
    public static void ProcessInbound
        ([Borrowed] ref IBufferLease current,
        ReadOnlySpan<byte> secret, CipherSuiteType algorithm, out uint? seq)
    {
        ArgumentNullException.ThrowIfNull(current);

        seq = null;
        IBufferLease original = current;
        PacketFlags flags = current.Span.AsHeaderRef().Flags;

        bool isEncrypted = (flags & PacketFlags.ENCRYPTED) != 0;
        bool isCompressed = (flags & PacketFlags.COMPRESSED) != 0;

        if (isEncrypted && isCompressed)
        {
            if (algorithm == CipherSuiteType.None)
            {
                Throw.EncryptedButNoCipher();
            }

            if (secret.IsEmpty)
            {
                Throw.EncryptedButNoKey();
            }

            ProcessInboundFused(ref current, secret, algorithm, out uint _seq);
            seq = _seq;
            return;
        }

        if (isEncrypted)
        {
            if (algorithm == CipherSuiteType.None)
            {
                Throw.EncryptedButNoCipher();
            }

            if (secret.IsEmpty)
            {
                Throw.EncryptedButNoKey();
            }

            current = FrameCipher.DecryptFrame(current, secret, algorithm, out uint _seq);
            seq = _seq;

            // Re-read flags after decryption since the inner payload might have other flags (e.g., COMPRESSED).
            flags = current.Span.AsHeaderRef().Flags;
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

    private static void ProcessInboundFused(
        [Borrowed] ref IBufferLease current,
        ReadOnlySpan<byte> secret, CipherSuiteType algorithm, out uint seq)
    {
        ReadOnlySpan<byte> srcSpan = current.Span;
        int decryptedSize = FrameTransformer.GetPlaintextLength(srcSpan);

        byte[] tempArr = BufferLease.ByteArrayPool.Rent(decryptedSize);
        try
        {
            Span<byte> tempRegion = tempArr.AsSpan(0, decryptedSize);

            // 1. Decrypt directly into tempRegion
            EnvelopeCipher.Decrypt(secret, srcSpan[FrameTransformer.Offset..], tempRegion, null, algorithm, out int decLen, out seq);

            // 2. Read the uncompressed length from the LZ4 block header
            int decompressedSize = FrameTransformer.GetDecompressedLength(tempRegion[..decLen]);

            // 3. Rent final lease for the fully decompressed packet
            BufferLease finalLease = BufferLease.Rent(FrameTransformer.Offset + decompressedSize);
            try
            {
                Span<byte> destFull = finalLease.SpanFull;

                // 4. Decompress from tempRegion into the final lease's payload section
                int finalLen = LZ4.LZ4Codec.Decode(tempRegion[..decLen], destFull[FrameTransformer.Offset..]);

                // 5. Copy the header once
                srcSpan[..FrameTransformer.Offset].CopyTo(destFull[..FrameTransformer.Offset]);

                // 6. Clear both ENCRYPTED and COMPRESSED flags from the final header
                ref PacketHeader header = ref destFull.AsHeaderRef();
                header.Flags &= ~(PacketFlags.COMPRESSED | PacketFlags.ENCRYPTED);

                // 7. Finalize the lease length
                finalLease.CommitLength(FrameTransformer.Offset + finalLen);

                // IMPORTANT: Only swap the reference.
                // DO NOT call current.Dispose() here to preserve the original lease ownership rule.
                current = finalLease;
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                finalLease.Dispose();
                throw;
            }
        }
        finally
        {
            // [SECURITY] Clear intermediate decrypted data (plaintext) from the pool array
            Array.Clear(tempArr, 0, decryptedSize);
            BufferLease.ByteArrayPool.Return(tempArr);
        }
    }

    /// <summary>
    /// Applies outbound transforms in transport order: compress first, then encrypt.
    /// Mutates the <paramref name="current"/> lease directly via <see langword="ref"/> to optimize performance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ProcessOutbound(
        [Borrowed] ref IBufferLease current, bool enableCompress,
        int minSizeToCompress, bool enableEncrypt, ReadOnlySpan<byte> secret, uint? seq, CipherSuiteType algorithm)
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
            if (seq == null)
            {
                Throw.EncryptRequestedButNoSeq();
            }

            ProcessOutboundFused(ref current, payloadSize, secret, seq.Value, algorithm);
            return;
        }

        if (doCompress)
        {
            current = FrameCompression.CompressFrame(current);
        }
        else if (enableEncrypt)
        {
            IBufferLease prev = current;
            current = FrameCipher.EncryptFrame(current, secret, seq, algorithm);

            // If we replaced a buffer that was ALREADY a replacement (intermediate),
            // we must dispose it to avoid a leak. We do NOT dispose the 'original' one.
            if (!ReferenceEquals(prev, original))
            {
                prev.Dispose();
            }
        }
    }

    private static void ProcessOutboundFused(
        [Borrowed] ref IBufferLease current,
        int payloadSize, ReadOnlySpan<byte> secret, uint seq, CipherSuiteType algorithm)
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
            EnvelopeCipher.Encrypt(secret, tempRegion[..compLen], finalRegion, null, seq, algorithm, out int encLen);

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
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            singleLease.Dispose();
            throw;
        }
    }
}
