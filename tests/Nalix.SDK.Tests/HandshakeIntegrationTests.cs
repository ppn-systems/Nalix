#if DEBUG
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Identity;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Primitives;
using Nalix.Common.Security;
using Nalix.Framework.DataFrames;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.Framework.Identifiers;
using Nalix.Framework.Random;
using Nalix.Framework.Security;
using Nalix.Framework.Security.Asymmetric;
using Nalix.Framework.Security.Hashing;
using Nalix.SDK.Options;
using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Extensions;
using Xunit;

namespace Nalix.SDK.Tests;

[Collection("RealServerTests")]
public sealed class HandshakeIntegrationTests : IDisposable
{
    private readonly TcpListener _listener;
    private readonly int _port;
    private readonly IPacketRegistry _registry;

    public HandshakeIntegrationTests()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        _port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _registry = new PacketRegistryFactory().CreateCatalog();
    }

    [Fact]
    public async Task HandshakeAsync_WithRealTcpConnection_Succeeds()
    {
        // 1. Setup Server Static Key
        X25519.X25519KeyPair serverStaticKey = X25519.GenerateKeyPair();

        using TcpSession session = new(new TransportOptions
        {
            EncryptionEnabled = false,
            ServerPublicKey = serverStaticKey.PublicKey.ToString()
        }, _registry);

        // 2. Start "Server" in background
        Task serverTask = RunMockHandshakeServerAsync(serverStaticKey);

        try
        {
            await session.ConnectAsync("127.0.0.1", (ushort)_port);

            // 3. Perform Handshake
            await session.HandshakeAsync(ct: new CancellationTokenSource(2000).Token);

            // 4. Verify Session State
            Assert.True(session.Options.EncryptionEnabled);
            Assert.NotEqual(Bytes32.Zero, session.Options.Secret);
            Assert.Equal(CipherSuiteType.Chacha20Poly1305, session.Options.Algorithm);
        }
        finally
        {
            await session.DisconnectAsync();
            await serverTask;
        }
    }

    [Fact]
    public async Task ResumeSessionAsync_WithRealTcpConnection_Succeeds()
    {
        // 1. Setup Server Static Key and state
        X25519.X25519KeyPair serverStaticKey = X25519.GenerateKeyPair();
        Span<byte> secretBytes = stackalloc byte[32];
        Csprng.Fill(secretBytes);
        Bytes32 sharedSecret = new(secretBytes);
        Snowflake token = Snowflake.NewId(SnowflakeType.Session);

        using TcpSession session = new(new TransportOptions
        {
            EncryptionEnabled = false,
            ServerPublicKey = serverStaticKey.PublicKey.ToString(),
            Secret = sharedSecret,
            SessionToken = token
        }, _registry);

        // 2. Start "Server" in background
        Task serverTask = RunMockHandshakeServerAsync(serverStaticKey, sharedSecret, token);

        try
        {
            await session.ConnectAsync("127.0.0.1", (ushort)_port);

            // 3. Perform Resume
            ProtocolReason result = await session.ResumeSessionAsync(ct: new CancellationTokenSource(2000).Token);

            // 4. Verify Result
            Assert.Equal(ProtocolReason.NONE, result);
            Assert.True(session.Options.EncryptionEnabled);
        }
        finally
        {
            await session.DisconnectAsync();
            await serverTask;
        }
    }

    [Fact]
    public async Task ConnectWithResumeAsync_WithRealTcpConnection_Succeeds()
    {
        // 1. Setup Server Static Key
        X25519.X25519KeyPair serverStaticKey = X25519.GenerateKeyPair();

        using TcpSession session = new(new TransportOptions
        {
            EncryptionEnabled = false,
            ServerPublicKey = serverStaticKey.PublicKey.ToString(),
            ResumeEnabled = true
        }, _registry);

        Bytes32 secret;
        Snowflake token;

        try
        {
            // 2. Perform Handshake first
            Task serverTask1 = RunMockHandshakeServerAsync(serverStaticKey);
            await session.ConnectWithResumeAsync("127.0.0.1", (ushort)_port);

            Assert.NotEqual(Snowflake.Empty, session.Options.SessionToken);
            secret = session.Options.Secret;
            token = session.Options.SessionToken;

            await session.DisconnectAsync();
            await serverTask1;
        }
        catch { throw; }

        // 3. Perform Resume
        Task serverTask2 = RunMockHandshakeServerAsync(serverStaticKey, secret, token);
        try
        {
            bool resumed = await session.ConnectWithResumeAsync("127.0.0.1", (ushort)_port);

            Assert.True(resumed);
            Assert.Equal(token, session.Options.SessionToken);
            Assert.True(session.Options.EncryptionEnabled);
        }
        finally
        {
            await session.DisconnectAsync();
            await serverTask2;
        }
    }

    private async Task RunMockHandshakeServerAsync(X25519.X25519KeyPair staticKey, Bytes32? resumeSecret = null, Snowflake? resumeToken = null)
    {
        using Socket serverSocket = await _listener.AcceptSocketAsync();
        byte[] lengthBuffer = new byte[2];

        // Loop to handle potential multi-step or multi-packet interaction
        while (true)
        {
            if (serverSocket.Poll(1000, SelectMode.SelectRead) && serverSocket.Available == 0)
            {
                break; // Socket closed
            }

            int received = 0;
            try
            {
                received = await serverSocket.ReceiveAsync(lengthBuffer, SocketFlags.None);
            }
            catch (SocketException) { break; }

            if (received <= 0) break;
            if (received != 2) break;

            ushort totalLen = BitConverter.ToUInt16(lengthBuffer, 0);
            byte[] payload = new byte[totalLen - 2];
            await serverSocket.ReceiveAsync(payload, SocketFlags.None);
            IPacket pkt = _registry.Deserialize(payload);

            if (pkt is Handshake clientHello && clientHello.Stage == HandshakeStage.CLIENT_HELLO)
            {
                // Handle Handshake... (existing logic)
                X25519.X25519KeyPair serverEphemeralKey = X25519.GenerateKeyPair();
                Span<byte> nonceBytes = stackalloc byte[32];
                Csprng.Fill(nonceBytes);
                Bytes32 serverNonce = new(nonceBytes);

                Bytes32 sharedSecretEE = X25519.Agreement(serverEphemeralKey.PrivateKey, clientHello.PublicKey);
                Bytes32 sharedSecretES = X25519.Agreement(staticKey.PrivateKey, clientHello.PublicKey);

                Bytes32 masterSecret = HandshakeX25519.ComputeMasterSecret(sharedSecretEE, sharedSecretES);
                Bytes32 transcriptHash = HandshakeX25519.ComputeTranscriptHash(
                    clientHello.PublicKey, clientHello.Nonce,
                    serverEphemeralKey.PublicKey, serverNonce);

                Bytes32 serverProof = HandshakeX25519.ComputeServerProof(masterSecret, transcriptHash);

                using Handshake serverHello = new(HandshakeStage.SERVER_HELLO, serverEphemeralKey.PublicKey, serverNonce, serverProof);
                serverHello.TranscriptHash = transcriptHash;
                await SendPacketAsync(serverSocket, serverHello);

                // Receive Finish
                Handshake clientFinish = await ReceivePacketAsync<Handshake>(serverSocket);

                Bytes32 finishProof = HandshakeX25519.ComputeServerFinishProof(masterSecret, transcriptHash);
                using Handshake serverFinish = new(HandshakeStage.SERVER_FINISH, Bytes32.Zero, Bytes32.Zero, finishProof);
                serverFinish.TranscriptHash = transcriptHash;
                serverFinish.SessionToken = Snowflake.NewId(SnowflakeType.Session);
                await SendPacketAsync(serverSocket, serverFinish);
            }
            else if (pkt is SessionResume resumeReq && resumeReq.Stage == SessionResumeStage.REQUEST)
            {
                // Verify Proof-of-Possession
                if (resumeSecret.HasValue && resumeToken.HasValue)
                {
                    Span<byte> expectedProof = stackalloc byte[32];
                    Span<byte> tokenBytes = stackalloc byte[8];
                    _ = resumeToken.Value.TryWriteBytes(tokenBytes);
                    HmacKeccak256.Compute(resumeSecret.Value.AsSpan(), tokenBytes[..7], expectedProof);

                    if (resumeReq.Proof == new Bytes32(expectedProof))
                    {
                        using SessionResume response = new();
                        response.Initialize(SessionResumeStage.RESPONSE, resumeToken.Value, ProtocolReason.NONE);
                        response.Proof = resumeReq.Proof; // Satisfy Validate()
                        await SendPacketAsync(serverSocket, response);
                    }
                    else
                    {
                        using SessionResume response = new();
                        response.Initialize(SessionResumeStage.RESPONSE, resumeToken.Value, ProtocolReason.UNAUTHENTICATED);
                        await SendPacketAsync(serverSocket, response);
                    }
                }
            }
        }
    }

    private async Task<TPkt> ReceivePacketAsync<TPkt>(Socket s) where TPkt : class, IPacket
    {
        byte[] lenBuf = new byte[2];
        await s.ReceiveAsync(lenBuf, SocketFlags.None);
        ushort totalLen = BitConverter.ToUInt16(lenBuf, 0);
        byte[] payload = new byte[totalLen - 2];
        await s.ReceiveAsync(payload, SocketFlags.None);
        return (TPkt)_registry.Deserialize(payload);
    }

    private async Task SendPacketAsync(Socket s, IPacket pkt)
    {
        byte[] data = new byte[pkt.Length];
        pkt.Serialize(data);
        byte[] frame = new byte[2 + data.Length];
        BitConverter.TryWriteBytes(frame.AsSpan(0, 2), (ushort)(2 + data.Length));
        data.CopyTo(frame.AsSpan(2));
        await s.SendAsync(frame, SocketFlags.None);
    }

    public void Dispose()
    {
        _listener.Stop();
    }
}

#endif
