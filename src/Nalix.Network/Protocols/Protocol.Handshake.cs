// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Security;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.Framework.Injection;
using Nalix.Framework.Random;
using Nalix.Framework.Security.Asymmetric;
using Nalix.Framework.Security.Hashing;

namespace Nalix.Network.Protocols;

/// <summary>
/// Default server-side handshake protocol for Nalix.
/// It performs an anonymous X25519 handshake, derives a shared session key, and
/// only forwards non-handshake packets to the dispatch pipeline once the session is established.
/// </summary>
[DebuggerDisplay("Accepting={IsAccepting}, KeepConnectionOpen={KeepConnectionOpen}")]
public sealed class HandshakeProtocol : Protocol
{
    private const string StateAttributeKey = "nalix.handshake.state";
    private const string EstablishedAttributeKey = "nalix.handshake.established";

    private static readonly ILogger? s_logger = InstanceManager.Instance.GetExistingInstance<ILogger>();

    private static ReadOnlySpan<byte> SessionLabel => "nalix-handshake/session"u8;
    private static ReadOnlySpan<byte> ServerProofLabel => "nalix-handshake/server-proof"u8;
    private static ReadOnlySpan<byte> ClientProofLabel => "nalix-handshake/client-proof"u8;
    private static ReadOnlySpan<byte> ServerFinishLabel => "nalix-handshake/server-finish"u8;

    /// <summary>
    /// Initializes a new instance of the <see cref="HandshakeProtocol"/> class.
    /// </summary>
    public HandshakeProtocol()
    {
        this.IsAccepting = true;
        this.KeepConnectionOpen = true;
    }

    /// <summary>
    /// Computes the server proof over the negotiated shared secret and transcript hash.
    /// </summary>
    public static byte[] ComputeServerProof(ReadOnlySpan<byte> sharedSecret, ReadOnlySpan<byte> transcriptHash)
        => ComputeDigest(ServerProofLabel, sharedSecret, transcriptHash);

    /// <summary>
    /// Computes the client proof over the negotiated shared secret and transcript hash.
    /// </summary>
    public static byte[] ComputeClientProof(ReadOnlySpan<byte> sharedSecret, ReadOnlySpan<byte> transcriptHash)
        => ComputeDigest(ClientProofLabel, sharedSecret, transcriptHash);

    /// <summary>
    /// Computes the final server acknowledgement proof over the negotiated shared secret and transcript hash.
    /// </summary>
    public static byte[] ComputeServerFinishProof(ReadOnlySpan<byte> sharedSecret, ReadOnlySpan<byte> transcriptHash)
        => ComputeDigest(ServerFinishLabel, sharedSecret, transcriptHash);

    /// <summary>
    /// Derives the session key that should be assigned to <see cref="IConnection.Secret"/>.
    /// </summary>
    public static byte[] DeriveSessionKey(
        ReadOnlySpan<byte> sharedSecret,
        ReadOnlySpan<byte> clientNonce,
        ReadOnlySpan<byte> serverNonce,
        ReadOnlySpan<byte> transcriptHash)
        => ComputeDigest(SessionLabel, sharedSecret, clientNonce, serverNonce, transcriptHash);

    /// <inheritdoc />
    public override void ProcessMessage(object? sender, IConnectEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        try
        {
            if (args.Lease is null)
            {
                return;
            }

            Handshake handshake = Handshake.Deserialize(args.Lease.Span);

            this.HandleHandshake(args.Connection, handshake);
        }
        finally
        {
            args.Lease?.Dispose();
            args.Dispose();
        }
    }

    private void HandleHandshake(IConnection connection, Handshake handshake)
    {
        switch (handshake.Stage)
        {
            case HandshakeStage.CLIENT_HELLO:
                this.HandleClientHello(connection, handshake);
                return;

            case HandshakeStage.CLIENT_FINISH:
                this.HandleClientFinish(connection, handshake);
                return;
            case HandshakeStage.NONE:
                break;
            case HandshakeStage.SERVER_HELLO:
                break;
            case HandshakeStage.SERVER_FINISH:
                break;
            case HandshakeStage.ERROR:
                break;
            default:
                this.Reject(connection, ProtocolReason.UNEXPECTED_MESSAGE);
                return;
        }
    }

    private void HandleClientHello(IConnection connection, Handshake packet)
    {
        if (!IsValidHelloPacket(packet))
        {
            this.Reject(connection, ProtocolReason.MALFORMED_PACKET);
            return;
        }

        X25519.X25519KeyPair serverKey = X25519.GenerateKeyPair();
        byte[] sharedSecret = X25519.Agreement(serverKey.PrivateKey, packet.PublicKey);
        if (IsAllZero(sharedSecret))
        {
            this.Reject(connection, ProtocolReason.DECRYPTION_FAILED);
            return;
        }

        byte[] serverNonce = Csprng.GetBytes(Handshake.DynamicSize);
        byte[] transcriptHash = Handshake.ComputeTranscriptHash(
            ComposeTranscriptBuffer(packet.PublicKey, packet.Nonce, serverKey.PublicKey, serverNonce));

        HandshakeSessionState state = new()
        {
            ClientPublicKey = packet.PublicKey,
            ClientNonce = packet.Nonce,
            SharedSecret = sharedSecret,
            ServerNonce = serverNonce,
            ServerPublicKey = serverKey.PublicKey,
            TranscriptHash = transcriptHash,
            SessionKey = DeriveSessionKey(sharedSecret, packet.Nonce, serverNonce, transcriptHash)
        };

        connection.Attributes[StateAttributeKey] = state;

        Handshake reply = new(packet.OpCode, HandshakeStage.SERVER_HELLO, serverKey.PublicKey, serverNonce, ComputeServerProof(sharedSecret, transcriptHash), packet.Protocol)
        {
            TranscriptHash = transcriptHash
        };

        connection.TCP.Send(reply);
    }

    private void HandleClientFinish(IConnection connection, Handshake packet)
    {
        if (!TryGetState(connection, out HandshakeSessionState? state) || state is null)
        {
            this.Reject(connection, ProtocolReason.STATE_VIOLATION);
            return;
        }

        if (packet.Proof.Length != Handshake.DynamicSize || packet.TranscriptHash.Length != Handshake.DynamicSize)
        {
            this.Reject(connection, ProtocolReason.MALFORMED_PACKET);
            return;
        }

        if (!CryptographicOperations.FixedTimeEquals(packet.TranscriptHash, state.TranscriptHash))
        {
            this.Reject(connection, ProtocolReason.CHECKSUM_FAILED);
            return;
        }

        byte[] expectedProof = ComputeClientProof(state.SharedSecret, state.TranscriptHash);
        if (!CryptographicOperations.FixedTimeEquals(packet.Proof, expectedProof))
        {
            this.Reject(connection, ProtocolReason.SIGNATURE_INVALID);
            return;
        }

        connection.Secret = state.SessionKey;
        connection.Algorithm = CipherSuiteType.Chacha20Poly1305;
        connection.Attributes[EstablishedAttributeKey] = true;
        _ = connection.Attributes.Remove(StateAttributeKey);

        Handshake reply = new(
            packet.OpCode,
            HandshakeStage.SERVER_FINISH,
            [],
            [],
            ComputeServerFinishProof(state.SharedSecret, state.TranscriptHash),
            packet.Protocol)
        {
            TranscriptHash = state.TranscriptHash
        };

        connection.TCP.Send(reply);
    }

    private void Reject(IConnection connection, ProtocolReason reason)
    {
        try
        {
            Control control = new();
            control.Initialize(opCode: 0, type: ControlType.ERROR, reasonCode: reason, transport: ProtocolType.TCP);
            connection.TCP.Send(control);
        }
        catch (Exception ex)
        {
            s_logger?.Debug($"[NW.{nameof(HandshakeProtocol)}] handshake-reject-send-failed id={connection.ID} ex={ex.Message}");
        }
        finally
        {
            connection.Disconnect(reason.ToString());
        }
    }

    private static bool TryGetState(IConnection connection, out HandshakeSessionState? state)
    {
        if (connection.Attributes.TryGetValue(StateAttributeKey, out object? boxed) && boxed is HandshakeSessionState typed)
        {
            state = typed;
            return true;
        }

        state = null;
        return false;
    }

    private static bool IsValidHelloPacket(Handshake packet)
        => packet.PublicKey.Length == Handshake.DynamicSize && packet.Nonce.Length == Handshake.DynamicSize;

    private static bool IsAllZero(ReadOnlySpan<byte> value)
    {
        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static byte[] ComposeTranscriptBuffer(
        ReadOnlySpan<byte> clientPublicKey,
        ReadOnlySpan<byte> clientNonce,
        ReadOnlySpan<byte> serverPublicKey,
        ReadOnlySpan<byte> serverNonce)
    {
        int total = (sizeof(int) * 4)
            + clientPublicKey.Length
            + clientNonce.Length
            + serverPublicKey.Length
            + serverNonce.Length;

        byte[] buffer = GC.AllocateUninitializedArray<byte>(total);
        Span<byte> destination = buffer;
        int offset = 0;

        offset = WriteSegment(destination, offset, clientPublicKey);
        offset = WriteSegment(destination, offset, clientNonce);
        offset = WriteSegment(destination, offset, serverPublicKey);
        _ = WriteSegment(destination, offset, serverNonce);

        return buffer;
    }

    private static byte[] ComputeDigest(
        ReadOnlySpan<byte> label,
        ReadOnlySpan<byte> segment0,
        ReadOnlySpan<byte> segment1)
    {
        int total = (sizeof(int) * 3) + label.Length + segment0.Length + segment1.Length;

        byte[] buffer = GC.AllocateUninitializedArray<byte>(total);
        Span<byte> destination = buffer;
        int offset = 0;

        offset = WriteSegment(destination, offset, label);
        offset = WriteSegment(destination, offset, segment0);
        _ = WriteSegment(destination, offset, segment1);

        return Keccak256.HashData(buffer);
    }

    private static byte[] ComputeDigest(
        ReadOnlySpan<byte> label,
        ReadOnlySpan<byte> segment0,
        ReadOnlySpan<byte> segment1,
        ReadOnlySpan<byte> segment2,
        ReadOnlySpan<byte> segment3)
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

        offset = WriteSegment(destination, offset, label);
        offset = WriteSegment(destination, offset, segment0);
        offset = WriteSegment(destination, offset, segment1);
        offset = WriteSegment(destination, offset, segment2);
        _ = WriteSegment(destination, offset, segment3);

        return Keccak256.HashData(buffer);
    }

    private static int WriteSegment(Span<byte> destination, int offset, ReadOnlySpan<byte> value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination[offset..], value.Length);
        offset += sizeof(int);
        value.CopyTo(destination[offset..]);
        offset += value.Length;
        return offset;
    }

    private sealed class HandshakeSessionState
    {
        public byte[] ClientPublicKey { get; init; } = [];
        public byte[] ClientNonce { get; init; } = [];
        public byte[] ServerPublicKey { get; init; } = [];
        public byte[] ServerNonce { get; init; } = [];
        public byte[] SharedSecret { get; init; } = [];
        public byte[] TranscriptHash { get; init; } = [];
        public byte[] SessionKey { get; init; } = [];
    }
}
