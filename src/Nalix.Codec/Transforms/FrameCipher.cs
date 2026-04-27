// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

#pragma warning disable IDE0079
#pragma warning disable CA1859

using System;
using System.Runtime.CompilerServices;
using Nalix.Abstractions;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Security;
using Nalix.Codec.Extensions;
using Nalix.Codec.Memory;
using Nalix.Codec.Security;

namespace Nalix.Codec.Transforms;

/// <summary>
/// Shared packet cipher helpers for encrypting and decrypting framed payloads.
/// </summary>
public static class FrameCipher
{
    /// <summary>
    /// Decrypts a framed packet and clears the encrypted flag in the resulting buffer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IBufferLease DecryptFrame([Borrowed] IBufferLease src, ReadOnlySpan<byte> key, CipherSuiteType expectedAlgorithm)
    {
        ArgumentNullException.ThrowIfNull(src);

        if (src.Length < FrameTransformer.Offset + EnvelopeCipher.HeaderSize)
        {
            throw new CipherException(
                $"Ciphertext frame is too short: length={src.Length}, required>={FrameTransformer.Offset + EnvelopeCipher.HeaderSize}.");
        }

        IBufferLease dest = BufferLease.Rent(FrameTransformer.Offset + FrameTransformer
                                       .GetPlaintextLength(src.Span));
        try
        {
            FrameTransformer.Decrypt(src, dest, key, expectedAlgorithm);
            dest.Span.WriteFlagsLE(dest.Span.ReadFlagsLE().RemoveFlag(PacketFlags.ENCRYPTED));
            return dest;
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            dest.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Encrypts a framed packet and sets the encrypted flag in the resulting buffer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IBufferLease EncryptFrame([Borrowed] IBufferLease src, ReadOnlySpan<byte> key, CipherSuiteType suite)
    {
        ArgumentNullException.ThrowIfNull(src);

        IBufferLease dest = BufferLease.Rent(FrameTransformer.Offset + FrameTransformer
                                       .GetMaxCiphertextSize(suite, src.Length - FrameTransformer.Offset));
        try
        {
            FrameTransformer.Encrypt(src, dest, key, suite);
            dest.Span.WriteFlagsLE(dest.Span.ReadFlagsLE().AddFlag(PacketFlags.ENCRYPTED));
            return dest;
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            dest.Dispose();
            throw;
        }
    }
}
