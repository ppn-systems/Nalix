using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using FluentAssertions;
using Nalix.Common.Identity;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Networking.Sessions;
using Nalix.Common.Security;
using Nalix.Framework.Configuration;
using Nalix.Framework.DataFrames;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.Framework.Identifiers;
using Nalix.Network.Connections;
using Nalix.Runtime.Options;
using Nalix.Runtime.Sessions;
using Nalix.SDK.Options;
using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Extensions;
using Xunit;
using WireProtocolType = Nalix.Common.Networking.Protocols.ProtocolType;

namespace Nalix.Network.Tests;

[CollectionDefinition("SessionResume", DisableParallelization = true)]
public sealed class SessionResumeTestGroup
{
}

[Collection("SessionResume")]
public sealed class SessionResumeTests
{
    [Fact]
    public async Task CreateSnapshot_AndResume_WithRotate_RebindsTokenAndRestoresState()
    {
        SessionManagerOptions options = ConfigurationManager.Instance.Get<SessionManagerOptions>();
        int previousTtl = options.SnapshotTtlMillis;
        bool previousRotate = options.RotateTokenOnResume;
        options.SnapshotTtlMillis = 30_000;
        options.RotateTokenOnResume = true;

        using SessionScope scope1 = await SessionScope.CreateAsync();
        using SessionScope scope2 = await SessionScope.CreateAsync();
        using Connection connection1 = new(scope1.ServerSocket);
        using Connection connection2 = new(scope2.ServerSocket);
        MemorySessionManager manager = new();

        try
        {
            connection1.Secret = [1, 2, 3, 4];
            connection1.Algorithm = CipherSuiteType.Salsa20Poly1305;
            connection1.Level = PermissionLevel.USER;
            connection1.Attributes["nalix.handshake.established"] = true;

            SessionSnapshot snapshot = manager.CreateSnapshot(connection1);
            snapshot.Secret.Should().Equal([1, 2, 3, 4]);
            snapshot.Attributes.Should().ContainKey("nalix.handshake.established");

            manager.TryGetSnapshot(snapshot.SessionToken, out SessionSnapshot? storedSnapshot).Should().BeTrue();
            storedSnapshot.Should().NotBeNull();
            storedSnapshot!.SessionToken.Should().Be(snapshot.SessionToken);

            SessionResumeResult resume = manager.TryResume(snapshot.SessionToken, connection2);

            resume.Success.Should().BeTrue();
            resume.TokenRotated.Should().BeTrue();
            resume.SessionToken.Should().NotBe(snapshot.SessionToken);
            manager.TryGetActiveConnection(resume.SessionToken, out IConnection? active).Should().BeTrue();
            active.Should().NotBeNull();
            active!.Should().BeSameAs(connection2);

            connection2.Secret.Should().Equal([1, 2, 3, 4]);
            connection2.Algorithm.Should().Be(CipherSuiteType.Salsa20Poly1305);
            connection2.Level.Should().Be(PermissionLevel.USER);
            ((bool)connection2.Attributes["nalix.handshake.established"]).Should().BeTrue();

            manager.TryGetSnapshot(snapshot.SessionToken, out _).Should().BeFalse();
        }
        finally
        {
            options.SnapshotTtlMillis = previousTtl;
            options.RotateTokenOnResume = previousRotate;
        }
    }

    [Fact]
    public async Task TryResume_WhenSnapshotExpires_ReturnsSessionExpired()
    {
        SessionManagerOptions options = ConfigurationManager.Instance.Get<SessionManagerOptions>();
        int previousTtl = options.SnapshotTtlMillis;
        bool previousRotate = options.RotateTokenOnResume;
        options.SnapshotTtlMillis = 1000;
        options.RotateTokenOnResume = false;

        using SessionScope scope1 = await SessionScope.CreateAsync();
        using SessionScope scope2 = await SessionScope.CreateAsync();
        using Connection connection1 = new(scope1.ServerSocket);
        using Connection connection2 = new(scope2.ServerSocket);
        MemorySessionManager manager = new();

        try
        {
            connection1.Secret = [9, 9, 9, 9];
            connection1.Attributes["nalix.handshake.established"] = true;

            SessionSnapshot snapshot = manager.CreateSnapshot(connection1);
            await Task.Delay(1100);

            SessionResumeResult resume = manager.TryResume(snapshot.SessionToken, connection2);

            resume.Success.Should().BeFalse();
            resume.Reason.Should().BeOneOf(ProtocolReason.SESSION_EXPIRED, ProtocolReason.SESSION_NOT_FOUND);
        }
        finally
        {
            options.SnapshotTtlMillis = previousTtl;
            options.RotateTokenOnResume = previousRotate;
        }
    }

    [Fact]
    public async Task TryResumeAsync_UpdatesClientTokenAndAlgorithm_FromServerAck()
    {
        using PacketRegistryFactoryScope registryScope = new();
        using Socket listener = new(AddressFamily.InterNetwork, SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
        listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        listener.Listen(1);
        int port = ((IPEndPoint)listener.LocalEndPoint!).Port;

        TransportOptions options = new()
        {
            Address = IPAddress.Loopback.ToString(),
            Port = (ushort)port,
            SessionToken = Snowflake.NewId(SnowflakeType.Session),
            Secret = [5, 4, 3, 2],
            Algorithm = CipherSuiteType.Chacha20Poly1305,
            EncryptionEnabled = false,
            ResumeEnabled = true,
            ResumeFallbackToHandshake = true,
            ResumeTimeoutMillis = 3000
        };

        TcpSession session = new(options, registryScope.Catalog);
        Snowflake expectedRequestToken = options.SessionToken;
        Snowflake responseToken = Snowflake.NewId(SnowflakeType.Session);

        Task serverTask = Task.Run(async () =>
        {
            using Socket accepted = await listener.AcceptAsync();

            SessionResume request = await ReceivePacketAsync<SessionResume>(accepted, registryScope.Catalog);
            request.SessionToken.Should().Be(expectedRequestToken);

            SessionResumeAck ack = new();
            ack.Initialize(
                success: true,
                reason: ProtocolReason.NONE,
                sessionToken: responseToken,
                algorithm: CipherSuiteType.Salsa20Poly1305,
                level: PermissionLevel.USER,
                transport: WireProtocolType.TCP);

            await SendPacketAsync(accepted, ack);
        });

        try
        {
            await session.ConnectAsync(options.Address, options.Port);

            bool resumed = await session.TryResumeAsync();

            resumed.Should().BeTrue();
            session.Options.SessionToken.Should().Be(responseToken);
            session.Options.Algorithm.Should().Be(CipherSuiteType.Salsa20Poly1305);
            session.Options.EncryptionEnabled.Should().BeTrue();

            await serverTask;
        }
        finally
        {
            session.Dispose();
        }
    }

    private static async Task<TPacket> ReceivePacketAsync<TPacket>(Socket socket, Nalix.Common.Networking.Packets.IPacketRegistry catalog)
        where TPacket : class, Nalix.Common.Networking.Packets.IPacket
    {
        byte[] header = new byte[TcpSession.HeaderSize];
        await ReceiveExactAsync(socket, header);

        ushort totalLength = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(header);
        byte[] payload = new byte[totalLength - TcpSession.HeaderSize];
        await ReceiveExactAsync(socket, payload);

        Nalix.Common.Networking.Packets.IPacket packet = catalog.Deserialize(payload);
        return Assert.IsType<TPacket>(packet);
    }

    private static async Task SendPacketAsync(Socket socket, Nalix.Common.Networking.Packets.IPacket packet)
    {
        byte[] body = new byte[packet.Length];
        int written = packet.Serialize(body);

        byte[] frame = new byte[TcpSession.HeaderSize + written];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(0, TcpSession.HeaderSize), (ushort)frame.Length);
        body.AsSpan(0, written).CopyTo(frame.AsSpan(TcpSession.HeaderSize));
        await socket.SendAsync(frame, SocketFlags.None);
    }

    private static async Task ReceiveExactAsync(Socket socket, byte[] buffer)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int received = await socket.ReceiveAsync(buffer.AsMemory(offset));
            if (received == 0)
            {
                throw new SocketException((int)SocketError.ConnectionReset);
            }

            offset += received;
        }
    }

    private sealed class PacketRegistryFactoryScope : IDisposable
    {
        public PacketRegistryFactoryScope()
        {
            Catalog = new PacketRegistryFactory().CreateCatalog();
        }

        public Nalix.Common.Networking.Packets.IPacketRegistry Catalog { get; }

        public void Dispose()
        {
        }
    }

    private sealed class SessionScope : IDisposable
    {
        private SessionScope(Socket listenerSocket, Socket clientSocket, Socket serverSocket)
        {
            ListenerSocket = listenerSocket;
            ClientSocket = clientSocket;
            ServerSocket = serverSocket;
        }

        public Socket ListenerSocket { get; }

        public Socket ClientSocket { get; }

        public Socket ServerSocket { get; }

        public static async Task<SessionScope> CreateAsync()
        {
            Socket listener = new(AddressFamily.InterNetwork, SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
            listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            listener.Listen(1);

            int port = ((IPEndPoint)listener.LocalEndPoint!).Port;
            Task<Socket> acceptTask = listener.AcceptAsync();

            Socket client = new(AddressFamily.InterNetwork, SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
            await client.ConnectAsync(IPAddress.Loopback, port);

            Socket server = await acceptTask;
            return new SessionScope(listener, client, server);
        }

        public void Dispose()
        {
            try { ClientSocket.Dispose(); } catch { }
            try { ServerSocket.Dispose(); } catch { }
            try { ListenerSocket.Dispose(); } catch { }
        }
    }
}
