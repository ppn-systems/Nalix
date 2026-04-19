using Nalix.Common.Networking.Protocols;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Abstractions;
using Nalix.Common.Exceptions;
using Nalix.Common.Identity;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Primitives;
using Nalix.Common.Security;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.Framework.Identifiers;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Security;
using Nalix.Framework.Security.Asymmetric;
using Nalix.SDK.Options;
using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Extensions;
using Xunit;

namespace Nalix.SDK.Tests;

[SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "xUnit tests intentionally follow the test synchronization context.")]
public sealed class SdkControlDisconnectHandshakeExtensionsTests
{
    private static readonly X25519.X25519KeyPair s_testServerKey = X25519.GenerateKeyPair();

    [Fact]
    public void NewControl_WithBuilderMethods_AppliesConfiguredValues()
    {
        FakeTransportSession session = new();

        Control packet = session.NewControl((ushort)ProtocolOpCode.SYSTEM_CONTROL, ControlType.NOTICE, reliable: true)
            .WithSeq(123)
            .WithReason(ProtocolReason.TIMEOUT)
            .WithReliable(true)
            .StampNow()
            .Build();

        Assert.Equal(ControlType.NOTICE, packet.Type);
        Assert.Equal(123u, packet.SequenceId);
        Assert.Equal(ProtocolReason.TIMEOUT, packet.Reason);
        Assert.True(packet.Flags.HasFlag(PacketFlags.RELIABLE));
        Assert.NotEqual(0L, packet.MonoTicks);
        Assert.True(packet.Timestamp > 0);
    }

    [Fact]
    public async Task AwaitPacketAsync_WhenClientIsNull_ThrowsArgumentNullException()
    {
        TcpSession? session = null;

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await session!.AwaitPacketAsync<Control>(_ => true, timeoutMs: 1000, ct: CancellationToken.None));
    }

    [Fact]
    public async Task AwaitPacketAsync_WhenPredicateIsNull_ThrowsArgumentNullException()
    {
        using TcpSession session = CreateDisconnectedTcpSession();

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await session.AwaitPacketAsync<Control>(null!, timeoutMs: 1000, ct: CancellationToken.None));
    }

    [Fact]
    public async Task AwaitPacketAsync_WhenClientNotConnected_ThrowsNetworkException()
    {
        using TcpSession session = CreateDisconnectedTcpSession();

        await Assert.ThrowsAsync<NetworkException>(async () =>
            await session.AwaitPacketAsync<Control>(_ => true, timeoutMs: 1000, ct: CancellationToken.None));
    }

    [Fact]
    public async Task SendControlAsync_WhenClientIsNull_ThrowsArgumentNullException()
    {
        TcpSession? session = null;

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await session!.SendControlAsync((ushort)ProtocolOpCode.SYSTEM_CONTROL, ControlType.PING, null, CancellationToken.None));
    }

    [Fact]
    public async Task SendControlAsync_WhenClientNotConnected_ThrowsNetworkException()
    {
        using TcpSession session = CreateDisconnectedTcpSession();

        await Assert.ThrowsAsync<NetworkException>(async () =>
            await session.SendControlAsync((ushort)ProtocolOpCode.SYSTEM_CONTROL, ControlType.PING, null, CancellationToken.None));
    }

    [Fact]
    public async Task DisconnectGracefullyAsync_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        TcpSession? session = null;

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await session!.DisconnectGracefullyAsync(ProtocolReason.NONE, closeLocalConnection: true, ct: CancellationToken.None));
    }

    [Fact]
    public async Task DisconnectGracefullyAsync_WhenDisconnected_StillCompletes()
    {
        using TcpSession session = CreateDisconnectedTcpSession();

        Exception? ex = await Record.ExceptionAsync(async () =>
            await session.DisconnectGracefullyAsync(ProtocolReason.NONE, closeLocalConnection: true, ct: CancellationToken.None));

        Assert.Null(ex);
    }

    [Fact]
    public async Task HandshakeAsync_WhenSessionIsNull_ThrowsArgumentNullException()
    {
        TransportSession? session = null;

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await session!.HandshakeAsync(CancellationToken.None));
    }

    [Fact]
    public async Task HandshakeAsync_WhenSessionDisconnected_ThrowsInvalidOperationException()
    {
        FakeTransportSession session = new() { Connected = false };

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await session.HandshakeAsync(CancellationToken.None));
    }

    [Fact]
    public async Task HandshakeAsync_WhenServerReturnsError_ThrowsNetworkException()
    {
        FakeTransportSession session = new();
        Handshake error = new()
        {
            Stage = HandshakeStage.ERROR,
            Reason = ProtocolReason.TIMEOUT
        };

        session.EnqueueResponse(error);

        NetworkException ex = await Assert.ThrowsAsync<NetworkException>(async () =>
            await session.HandshakeAsync(CancellationToken.None));

        Assert.Contains("Handshake failed", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandshakeAsync_WhenServerHelloMalformed_ThrowsNetworkException()
    {
        FakeTransportSession session = new();
        Handshake malformed = new(HandshakeStage.SERVER_HELLO, Bytes32.Zero, Bytes32.Zero, flags: PacketFlags.SYSTEM | PacketFlags.RELIABLE);
        session.EnqueueResponse(malformed);

        NetworkException ex = await Assert.ThrowsAsync<NetworkException>(async () =>
            await session.HandshakeAsync(CancellationToken.None));

        Assert.Contains("Malformed Handshake SERVER_HELLO packet", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandshakeAsync_WhenServerProofInvalid_ThrowsNetworkException()
    {
        FakeTransportSession session = new();
        session.SendInterceptor = packet =>
        {
            if (packet is not Handshake clientHello || clientHello.Stage != HandshakeStage.CLIENT_HELLO)
            {
                return null;
            }

            X25519.X25519KeyPair serverKey = X25519.GenerateKeyPair();
            Bytes32 serverNonce = RandomBytes32();
            Handshake badServerHello = new(HandshakeStage.SERVER_HELLO, serverKey.PublicKey, serverNonce, flags: PacketFlags.SYSTEM | PacketFlags.RELIABLE)
            {
                Proof = RandomBytes32(),
                TranscriptHash = RandomBytes32()
            };
            return badServerHello;
        };

        using CancellationTokenSource cts = new();
        NetworkException ex = await Assert.ThrowsAsync<NetworkException>(async () =>
            await session.HandshakeAsync(cts.Token));

        Assert.Contains("Handshake SERVER_HELLO proof is invalid", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandshakeAsync_WhenServerResponsesAreValid_UpdatesSessionSecurityState()
    {
        FakeTransportSession session = new();
        X25519.X25519KeyPair serverKey = X25519.GenerateKeyPair();
        Bytes32 serverNonce = RandomBytes32();
        Bytes32 sharedSecret = default;
        Bytes32 transcriptHash = default;
        Snowflake token = Snowflake.NewId(SnowflakeType.System);
        Bytes32 contextPublicKey = default;

        session.SendInterceptor = packet =>
        {
            if (packet is Handshake clientHello && clientHello.Stage == HandshakeStage.CLIENT_HELLO)
            {
                contextPublicKey = clientHello.PublicKey;
                sharedSecret = X25519.Agreement(serverKey.PrivateKey, clientHello.PublicKey);
                
                // Use the matching static secret for the pinned server key
                Bytes32 staticSecret = X25519.Agreement(s_testServerKey.PrivateKey, clientHello.PublicKey);
                
                Bytes32 masterSecret = HandshakeX25519.ComputeMasterSecret(sharedSecret, staticSecret);
                transcriptHash = Handshake.ComputeTranscriptHash(
                    HandshakeX25519.ComposeTranscriptBuffer(
                        clientHello.PublicKey,
                        clientHello.Nonce,
                        serverKey.PublicKey,
                        serverNonce));

                return new Handshake(HandshakeStage.SERVER_HELLO, serverKey.PublicKey, serverNonce, flags: clientHello.Flags)
                {
                    Proof = HandshakeX25519.ComputeServerProof(masterSecret, transcriptHash),
                    TranscriptHash = transcriptHash
                };
            }

            if (packet is Handshake clientFinish && clientFinish.Stage == HandshakeStage.CLIENT_FINISH)
            {
                Bytes32 staticSecret = X25519.Agreement(s_testServerKey.PrivateKey, contextPublicKey);
                Bytes32 masterSecretFinish = HandshakeX25519.ComputeMasterSecret(sharedSecret, staticSecret);
                return new Handshake(HandshakeStage.SERVER_FINISH, Bytes32.Zero, Bytes32.Zero, flags: clientFinish.Flags)
                {
                    Proof = HandshakeX25519.ComputeServerFinishProof(masterSecretFinish, transcriptHash),
                    TranscriptHash = transcriptHash,
                    SessionToken = token
                };
            }

            return null;
        };

        await session.HandshakeAsync(CancellationToken.None);

        Assert.True(session.Options.EncryptionEnabled);
        Assert.Equal(CipherSuiteType.Chacha20Poly1305, session.Options.Algorithm);
        Assert.False(session.Options.Secret.IsZero);
        Assert.Equal(token, session.Options.SessionToken);
    }

    private static TcpSession CreateDisconnectedTcpSession()
        => new(new TransportOptions(), new FakePacketRegistry());

    private static Bytes32 RandomBytes32()
    {
        X25519.X25519KeyPair key = X25519.GenerateKeyPair();
        return key.PublicKey;
    }

    private sealed class FakeTransportSession : TransportSession
    {
        private readonly FakePacketRegistry _catalog = new();
        private readonly Queue<IPacket> _queuedResponses = new();

        public override TransportOptions Options { get; } = new TransportOptions
        {
            ServerPublicKey = s_testServerKey.PublicKey.ToString()
        };
        public override IPacketRegistry Catalog => _catalog;
        public override bool IsConnected => Connected;
        public bool Connected { get; set; } = true;

        public Func<IPacket, IPacket?>? SendInterceptor { get; set; }

        public override event EventHandler? OnConnected
        {
            add { }
            remove { }
        }

        public override event EventHandler<Exception>? OnDisconnected;
        public override event EventHandler<IBufferLease>? OnMessageReceived;

        public override event EventHandler<Exception>? OnError
        {
            add { }
            remove { }
        }

        public override Task ConnectAsync(string? host = null, ushort? port = null, CancellationToken ct = default)
            => Task.CompletedTask;

        public override Task DisconnectAsync()
        {
            Connected = false;
            OnDisconnected?.Invoke(this, new InvalidOperationException("disconnect"));
            return Task.CompletedTask;
        }

        public override Task SendAsync(IPacket packet, CancellationToken ct = default)
        {
            IPacket? response = SendInterceptor?.Invoke(packet);
            if (response is null && _queuedResponses.Count > 0)
            {
                response = _queuedResponses.Dequeue();
            }

            if (response is not null)
            {
                _catalog.NextPacket = response;
                using BufferLease lease = BufferLease.CopyFrom([1, 2, 3]);
                OnMessageReceived?.Invoke(this, lease);
            }

            return Task.CompletedTask;
        }

        public override Task SendAsync(ReadOnlyMemory<byte> payload, CancellationToken ct = default)
            => Task.CompletedTask;

        public void EnqueueResponse(IPacket packet) => _queuedResponses.Enqueue(packet);

        public override void Dispose()
        {
        }
    }

    private sealed class FakePacketRegistry : IPacketRegistry
    {
        public IPacket? NextPacket { get; set; }

        public int DeserializerCount => 1;
        public bool IsKnownMagic(uint magic) => true;
        public bool IsRegistered<TPacket>() where TPacket : IPacket => true;

        public IPacket Deserialize(ReadOnlySpan<byte> raw)
            => NextPacket ?? new Control();

        public bool TryDeserialize(ReadOnlySpan<byte> raw, [NotNullWhen(true)] out IPacket? packet)
        {
            packet = NextPacket ?? new Control();
            return true;
        }
    }
}

