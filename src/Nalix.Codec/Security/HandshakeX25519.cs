// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Primitives;
using Nalix.Codec.Security.Hashing;
using Nalix.Codec.Security.Primitives;

namespace Nalix.Codec.Security;

/// <summary>
/// Provides shared cryptographic primitives and utility methods for the Nalix handshake protocol.
/// These methods are used by both the client and server sides of the X25519 handshake flow.
/// </summary>
public static class HandshakeX25519
{
    #region Static Labels

    private static ReadOnlySpan<byte> SessionLabel => "nalix-handshake/session"u8;
    private static ReadOnlySpan<byte> ServerProofLabel => "nalix-handshake/server-proof"u8;
    private static ReadOnlySpan<byte> ClientProofLabel => "nalix-handshake/client-proof"u8;
    private static ReadOnlySpan<byte> ServerFinishLabel => "nalix-handshake/server-finish"u8;
    private static ReadOnlySpan<byte> MasterSecretLabel => "nalix-handshake/master-secret"u8;

    #endregion Static Labels

    #region HKDF-Keccak256 Helpers (RFC 5869)

    private static class Hkdf
    {
        /// <summary>
        /// HKDF-Extract: PRK = HMAC-Hash(salt, IKM)
        /// </summary>
        public static Bytes32 Extract(ReadOnlySpan<byte> salt, ReadOnlySpan<byte> ikm)
        {
            Span<byte> prk = stackalloc byte[32];
            HmacKeccak256.Compute(salt, ikm, prk);
            return new Bytes32(prk);
        }

        public static Bytes32 Expand(Bytes32 prk, ReadOnlySpan<byte> info)
        {
            Span<byte> okm = stackalloc byte[32];
            Span<byte> t = stackalloc byte[info.Length + 1];

            info.CopyTo(t);
            t[^1] = 1; // counter

            HmacKeccak256.Compute(prk.AsSpan(), t, okm);
            return new Bytes32(okm);
        }
    }

    #endregion HKDF-Keccak256 Helpers (RFC 5869)

    #region Public Methods

    /// <summary>
    /// Computes the master secret by combining the ephemeral-ephemeral and static-ephemeral shared secrets.
    /// This provides both forward secrecy and server authentication (MitM protection) via Noise Protocol concepts.
    /// </summary>
    public static Bytes32 ComputeMasterSecret(Bytes32 sharedSecretEE, Bytes32 sharedSecretSE)
    {
        Span<byte> ikm = stackalloc byte[64];
        sharedSecretEE.WriteTo(ikm);
        sharedSecretSE.WriteTo(ikm[32..]);

        return Hkdf.Extract(ReadOnlySpan<byte>.Empty, ikm);
    }

    /// <summary>
    /// Computes the server proof over the negotiated shared secret and transcript hash.
    /// </summary>
    public static Bytes32 ComputeServerProof(Bytes32 masterSecret, Bytes32 transcriptHash)
    {
        Span<byte> info = stackalloc byte[ServerProofLabel.Length + 32];
        ServerProofLabel.CopyTo(info);
        transcriptHash.WriteTo(info[ServerProofLabel.Length..]);
        return Hkdf.Expand(masterSecret, info);
    }

    /// <summary>
    /// Computes the client proof over the negotiated shared secret and transcript hash.
    /// </summary>
    public static Bytes32 ComputeClientProof(Bytes32 masterSecret, Bytes32 transcriptHash)
    {
        Span<byte> info = stackalloc byte[ClientProofLabel.Length + 32];
        ClientProofLabel.CopyTo(info);
        transcriptHash.WriteTo(info[ClientProofLabel.Length..]);
        return Hkdf.Expand(masterSecret, info);
    }

    /// <summary>
    /// Computes the final server acknowledgement proof over the negotiated shared secret and transcript hash.
    /// </summary>
    public static Bytes32 ComputeServerFinishProof(Bytes32 masterSecret, Bytes32 transcriptHash)
    {
        Span<byte> info = stackalloc byte[ServerFinishLabel.Length + 32];
        ServerFinishLabel.CopyTo(info);
        transcriptHash.WriteTo(info[ServerFinishLabel.Length..]);
        return Hkdf.Expand(masterSecret, info);
    }

    /// <summary>
    /// Derives the session key that should be assigned to the connection.
    /// </summary>
    public static Bytes32 DeriveSessionKey(Bytes32 masterSecret, Bytes32 clientNonce, Bytes32 serverNonce, Bytes32 transcriptHash)
    {
        Span<byte> info = stackalloc byte[SessionLabel.Length + 96];
        int offset = 0;

        SessionLabel.CopyTo(info);
        offset += SessionLabel.Length;

        clientNonce.WriteTo(info[offset..]);
        offset += 32;
        serverNonce.WriteTo(info[offset..]);
        offset += 32;
        transcriptHash.WriteTo(info[offset..]);

        return Hkdf.Expand(masterSecret, info[..(offset + 32)]);
    }

    /// <summary>
    /// Composes the initial transcript buffer from public keys and nonces to compute the transcript hash.
    /// </summary>
    /// <remarks>
    /// This API is kept for compatibility and returns raw transcript bytes.
    /// Callers should wipe the returned buffer with
    /// <see cref="MemorySecurity.ZeroMemory(byte[])"/> after hashing.
    /// </remarks>
    public static byte[] ComposeTranscriptBuffer(Bytes32 clientPublicKey, Bytes32 clientNonce, Bytes32 serverPublicKey, Bytes32 serverNonce)
    {
        int total = (sizeof(int) * 4)
            + Bytes32.Size
            + Bytes32.Size
            + Bytes32.Size
            + Bytes32.Size;

        byte[] buffer = GC.AllocateUninitializedArray<byte>(total);
        Span<byte> destination = buffer;
        int offset = 0;

        try
        {
            offset = WriteSegment(destination, offset, clientPublicKey.AsSpan());
            offset = WriteSegment(destination, offset, clientNonce.AsSpan());
            offset = WriteSegment(destination, offset, serverPublicKey.AsSpan());
            _ = WriteSegment(destination, offset, serverNonce.AsSpan());

            return buffer;
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            MemorySecurity.ZeroMemory(buffer);
            throw;
        }
    }

    /// <summary>
    /// Computes the handshake transcript hash from public keys and nonces,
    /// and securely clears the temporary heap buffer after hashing.
    /// </summary>
    public static Bytes32 ComputeTranscriptHash(
        Bytes32 clientPublicKey, Bytes32 clientNonce,
        Bytes32 serverPublicKey, Bytes32 serverNonce)
    {
        byte[] buffer = ComposeTranscriptBuffer(clientPublicKey, clientNonce, serverPublicKey, serverNonce);
        try
        {
            return Keccak256.HashDataToFixed(buffer);
        }
        finally
        {
            MemorySecurity.ZeroMemory(buffer);
        }
    }

    #endregion Public Methods

    #region Private Helpers

    private static int WriteSegment(Span<byte> destination, int offset, ReadOnlySpan<byte> value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination[offset..], value.Length);
        offset += sizeof(int);
        value.CopyTo(destination[offset..]);
        offset += value.Length;
        return offset;
    }

    #endregion Private Helpers
}
