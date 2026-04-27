// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using Nalix.Codec.Security.Hashing;
using Nalix.Codec.Security.Primitives;
using Nalix.Abstractions.Primitives;

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

    #region Public Methods

    /// <summary>
    /// Computes the master secret by combining the ephemeral-ephemeral and static-ephemeral shared secrets.
    /// This provides both forward secrecy and server authentication (MitM protection) via Noise Protocol concepts.
    /// </summary>
    public static Bytes32 ComputeMasterSecret(Bytes32 sharedSecretEE, Bytes32 sharedSecretSE)
        => ComputeDigest(MasterSecretLabel, sharedSecretEE.AsSpan(), sharedSecretSE.AsSpan());

    /// <summary>
    /// Computes the server proof over the negotiated shared secret and transcript hash.
    /// </summary>
    public static Bytes32 ComputeServerProof(Bytes32 sharedSecret, Bytes32 transcriptHash)
        => ComputeDigest(ServerProofLabel, sharedSecret.AsSpan(), transcriptHash.AsSpan());

    /// <summary>
    /// Computes the client proof over the negotiated shared secret and transcript hash.
    /// </summary>
    public static Bytes32 ComputeClientProof(Bytes32 sharedSecret, Bytes32 transcriptHash)
        => ComputeDigest(ClientProofLabel, sharedSecret.AsSpan(), transcriptHash.AsSpan());

    /// <summary>
    /// Computes the final server acknowledgement proof over the negotiated shared secret and transcript hash.
    /// </summary>
    public static Bytes32 ComputeServerFinishProof(Bytes32 sharedSecret, Bytes32 transcriptHash)
        => ComputeDigest(ServerFinishLabel, sharedSecret.AsSpan(), transcriptHash.AsSpan());

    /// <summary>
    /// Derives the session key that should be assigned to the connection.
    /// </summary>
    public static Bytes32 DeriveSessionKey(Bytes32 sharedSecret, Bytes32 clientNonce, Bytes32 serverNonce, Bytes32 transcriptHash)
        => ComputeDigest(SessionLabel, sharedSecret.AsSpan(), clientNonce.AsSpan(), serverNonce.AsSpan(), transcriptHash.AsSpan());

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
        catch (Exception ex) when (Abstractions.Exceptions.ExceptionClassifier.IsNonFatal(ex))
        {
            MemorySecurity.ZeroMemory(buffer);
            throw;
        }
    }

    /// <summary>
    /// Computes the handshake transcript hash from public keys and nonces,
    /// and securely clears the temporary heap buffer after hashing.
    /// </summary>
    public static Bytes32 ComputeTranscriptHash(Bytes32 clientPublicKey, Bytes32 clientNonce, Bytes32 serverPublicKey, Bytes32 serverNonce)
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

    private static Bytes32 ComputeDigest(ReadOnlySpan<byte> label, ReadOnlySpan<byte> segment0, ReadOnlySpan<byte> segment1)
    {
        int total = (sizeof(int) * 3) + label.Length + segment0.Length + segment1.Length;

        byte[] buffer = GC.AllocateUninitializedArray<byte>(total);
        Span<byte> destination = buffer;
        int offset = 0;

        try
        {
            offset = WriteSegment(destination, offset, label);
            offset = WriteSegment(destination, offset, segment0);
            _ = WriteSegment(destination, offset, segment1);

            return Keccak256.HashDataToFixed(buffer);
        }
        finally
        {
            MemorySecurity.ZeroMemory(buffer);
        }
    }

    private static Bytes32 ComputeDigest(ReadOnlySpan<byte> label, ReadOnlySpan<byte> segment0, ReadOnlySpan<byte> segment1, ReadOnlySpan<byte> segment2, ReadOnlySpan<byte> segment3)
    {
        int total = (sizeof(int) * 5)
            + label.Length
            + segment0.Length
            + segment1.Length
            + segment2.Length
            + segment3.Length;

        byte[] buffer = GC.AllocateUninitializedArray<byte>(total);
        Span<byte> destination = buffer;
        int offset = 0;

        try
        {
            offset = WriteSegment(destination, offset, label);
            offset = WriteSegment(destination, offset, segment0);
            offset = WriteSegment(destination, offset, segment1);
            offset = WriteSegment(destination, offset, segment2);
            _ = WriteSegment(destination, offset, segment3);

            return Keccak256.HashDataToFixed(buffer);
        }
        finally
        {
            MemorySecurity.ZeroMemory(buffer);
        }
    }

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
