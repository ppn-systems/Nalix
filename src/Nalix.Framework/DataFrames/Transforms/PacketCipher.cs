// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using Nalix.Common.Abstractions;
using Nalix.Common.Exceptions;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Security;
using Nalix.Framework.Extensions;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Security;

namespace Nalix.Framework.DataFrames.Transforms;

/// <summary>
/// Shared packet cipher helpers for encrypting and decrypting framed payloads.
/// </summary>
public static class PacketCipher
{
    /// <summary>
    /// Decrypts a framed packet and clears the encrypted flag in the resulting buffer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IBufferLease DecryptFrame([Borrowed] IBufferLease src, ReadOnlySpan<byte> key)
    {
        ArgumentNullException.ThrowIfNull(src);

        if (src.Length < FrameTransformer.Offset + EnvelopeCipher.HeaderSize)
        {
            throw new CipherException(
                $"Ciphertext frame is too short: length={src.Length}, required>={FrameTransformer.Offset + EnvelopeCipher.HeaderSize}.");
        }

        BufferLease dest = BufferLease.Rent(FrameTransformer
                                      .GetPlaintextLength(src.Span));
        try
        {
            FrameTransformer.Decrypt(src, dest, key);
            dest.Span.WriteFlagsLE(dest.Span.ReadFlagsLE().RemoveFlag(PacketFlags.ENCRYPTED));
            return dest;
        }
        catch
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

        BufferLease dest = BufferLease.Rent(FrameTransformer
                                      .GetMaxCiphertextSize(suite, src.Length - FrameTransformer.Offset));
        try
        {
            FrameTransformer.Encrypt(src, dest, key, suite);
            dest.Span.WriteFlagsLE(dest.Span.ReadFlagsLE().AddFlag(PacketFlags.ENCRYPTED));
            return dest;
        }
        catch
        {
            dest.Dispose();
            throw;
        }
    }
}
