using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Abstractions;
using Nalix.Common.Identity;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Security;
using Nalix.Framework.DataFrames;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.Framework.Identifiers;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Objects;
using Nalix.Framework.Random;
using Nalix.Framework.Security.Asymmetric;
using Nalix.Network.Protocols;
using Nalix.Network.Routing;
using Xunit;

namespace Nalix.Network.Tests;

public sealed class DefaultHandshakeProtocolTests
{
    public DefaultHandshakeProtocolTests()
    {
        _ = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>();
        InstanceManager.Instance.Register<IPacketRegistry>(new PacketRegistry(factory =>
        {
            _ = factory.RegisterPacket<Handshake>();
            _ = factory.RegisterPacket<Control>();
        }));
    }

    [Fact]
    public void ProcessMessage_ClientHello_SendsServerHello()
    {
        FakePacketDispatch dispatch = new();
        DefaultHandshakeProtocol protocol = new(dispatch);
        FakeConnection connection = new();

        X25519.X25519KeyPair clientKey = X25519.GenerateKeyPair();
        byte[] clientNonce = Csprng.GetBytes(Handshake.DynamicSize);
        Handshake clientHello = new(1, HandshakeStage.CLIENT_HELLO, clientKey.PublicKey, clientNonce, transport: ProtocolType.TCP);

        using TestConnectEventArgs args = new(Serialize(clientHello), connection);
        protocol.ProcessMessage(null, args);

        Handshake reply = Assert.IsType<Handshake>(Assert.Single(connection.TcpTransport.SentPackets));
        Assert.Equal(HandshakeStage.SERVER_HELLO, reply.Stage);
        Assert.Equal(Handshake.DynamicSize, reply.PublicKey.Length);
        Assert.Equal(Handshake.DynamicSize, reply.Nonce.Length);
        Assert.Equal(Handshake.DynamicSize, reply.Proof.Length);
        Assert.Equal(Handshake.DynamicSize, reply.TranscriptHash.Length);
        Assert.Empty(connection.Secret);
        Assert.Equal(0, dispatch.LeaseDispatchCount);
        Assert.Equal(0, dispatch.PacketDispatchCount);
    }

    [Fact]
    public void ProcessMessage_ClientFinish_CompletesHandshakeAndDispatchesNextPacket()
    {
        FakePacketDispatch dispatch = new();
        DefaultHandshakeProtocol protocol = new(dispatch);
        FakeConnection connection = new();

        X25519.X25519KeyPair clientKey = X25519.GenerateKeyPair();
        byte[] clientNonce = Csprng.GetBytes(Handshake.DynamicSize);
        Handshake clientHello = new(7, HandshakeStage.CLIENT_HELLO, clientKey.PublicKey, clientNonce, transport: ProtocolType.TCP);

        using (TestConnectEventArgs helloArgs = new(Serialize(clientHello), connection))
        {
            protocol.ProcessMessage(null, helloArgs);
        }

        Handshake serverHello = Assert.IsType<Handshake>(connection.TcpTransport.SentPackets[^1]);
        byte[] sharedSecret = X25519.Agreement(clientKey.PrivateKey, serverHello.PublicKey);
        byte[] clientProof = DefaultHandshakeProtocol.ComputeClientProof(sharedSecret, serverHello.TranscriptHash);
        byte[] expectedSessionKey = DefaultHandshakeProtocol.DeriveSessionKey(sharedSecret, clientNonce, serverHello.Nonce, serverHello.TranscriptHash);

        Handshake clientFinish = new(7, HandshakeStage.CLIENT_FINISH, [], [], clientProof, ProtocolType.TCP)
        {
            TranscriptHash = serverHello.TranscriptHash
        };

        using (TestConnectEventArgs finishArgs = new(Serialize(clientFinish), connection))
        {
            protocol.ProcessMessage(null, finishArgs);
        }

        Handshake serverFinish = Assert.IsType<Handshake>(connection.TcpTransport.SentPackets[^1]);
        Assert.Equal(HandshakeStage.SERVER_FINISH, serverFinish.Stage);
        Assert.Equal(expectedSessionKey, connection.Secret);
        Assert.Equal(CipherSuiteType.Chacha20Poly1305, connection.Algorithm);

        Control applicationPacket = new();
        applicationPacket.Initialize(99, ControlType.PING, reasonCode: ProtocolReason.NONE, transport: ProtocolType.TCP);

        using (TestConnectEventArgs appArgs = new(Serialize(applicationPacket), connection))
        {
            protocol.ProcessMessage(null, appArgs);
        }

        Assert.Equal(1, dispatch.LeaseDispatchCount);
    }

    private static IBufferLease Serialize(IPacket packet)
    {
        byte[] bytes = packet.Serialize();
        return TestBufferLease.FromBytes(bytes);
    }

    private sealed class FakePacketDispatch : IPacketDispatch
    {
        public int LeaseDispatchCount { get; private set; }
        public int PacketDispatchCount { get; private set; }

        public void Activate(CancellationToken cancellationToken = default)
        {
        }

        public void Deactivate(CancellationToken cancellationToken = default)
        {
        }

        public string GenerateReport() => nameof(FakePacketDispatch);

        public IDictionary<string, object> GetReportData() => new Dictionary<string, object>();

        public void HandlePacket(IBufferLease packet, IConnection connection) => LeaseDispatchCount++;

        public void HandlePacket(IPacket packet, IConnection connection) => PacketDispatchCount++;

        public void Dispose()
        {
        }
    }

    private sealed class FakeConnection : IConnection
    {
        public FakeConnection()
        {
            this.ID = Snowflake.NewId(SnowflakeType.Session);
            this.Attributes = ObjectMap<string, object>.Rent();
            this.NetworkEndpoint = new TestEndpoint("127.0.0.1");
            this.TcpTransport = new FakeTcpTransport();
            this.UdpTransport = new FakeUdpTransport();
            this.Secret = [];
        }

        public ISnowflake ID { get; }
        public long UpTime => 0;
        public long BytesSent => 0;
        public long LastPingTime => 0;
        public INetworkEndpoint NetworkEndpoint { get; }
        public IObjectMap<string, object> Attributes { get; }
        public byte[] Secret { get; set; }
        public PermissionLevel Level { get; set; } = PermissionLevel.NONE;
        public CipherSuiteType Algorithm { get; set; } = CipherSuiteType.Chacha20Poly1305;
        public int ErrorCount { get; private set; }
        public event EventHandler<IConnectEventArgs>? OnCloseEvent { add { } remove { } }
        public event EventHandler<IConnectEventArgs>? OnProcessEvent { add { } remove { } }
        public event EventHandler<IConnectEventArgs>? OnPostProcessEvent { add { } remove { } }
        public FakeTcpTransport TcpTransport { get; }
        public FakeUdpTransport UdpTransport { get; }
        public bool Disconnected { get; private set; }

        public IConnection.ITcp TCP => TcpTransport;
        public IConnection.IUdp UDP => UdpTransport;

        public void IncrementErrorCount() => ErrorCount++;

        public void Close(bool force = false) => Disconnected = true;

        public void Disconnect(string? reason = null) => Disconnected = true;

        public IConnection.IUdp GetOrCreateUDP(ref System.Net.IPEndPoint iPEndPoint) => UdpTransport;

        public void Dispose()
        {
            this.Attributes.Return();
        }
    }

    private sealed class FakeTcpTransport : IConnection.ITcp
    {
        public List<IPacket> SentPackets { get; } = [];

        public void BeginReceive(CancellationToken cancellationToken = default)
        {
        }

        public void Send(IPacket packet) => SentPackets.Add(packet);

        public void Send(ReadOnlySpan<byte> message)
        {
        }

        public Task SendAsync(IPacket packet, CancellationToken cancellationToken = default)
        {
            SentPackets.Add(packet);
            return Task.CompletedTask;
        }

        public Task SendAsync(ReadOnlyMemory<byte> message, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public void Send(string message)
        {
        }

        public Task SendAsync(string message, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeUdpTransport : IConnection.IUdp
    {
        public void Initialize(ref System.Net.IPEndPoint iPEndPoint)
        {
        }

        public void Send(IPacket packet)
        {
        }

        public void Send(ReadOnlySpan<byte> message)
        {
        }

        public Task SendAsync(IPacket packet, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SendAsync(ReadOnlyMemory<byte> message, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class TestConnectEventArgs(IBufferLease lease, IConnection connection) : IConnectEventArgs
    {
        public IConnection Connection { get; } = connection;
        public IBufferLease? Lease { get; } = lease;
        public INetworkEndpoint? NetworkEndpoint => this.Connection.NetworkEndpoint;

        public void Dispose() => this.Lease?.Dispose();
    }

    private sealed class TestBufferLease(byte[] buffer) : IBufferLease
    {
        private int _length = buffer.Length;

        public int Length => _length;
        public int Capacity => buffer.Length;
        public Span<byte> Span => buffer.AsSpan(0, _length);
        public Span<byte> SpanFull => buffer;
        public ReadOnlyMemory<byte> Memory => buffer.AsMemory(0, _length);

        public static TestBufferLease FromBytes(byte[] bytes) => new([.. bytes]);

        public void Retain()
        {
        }

        public void CommitLength(int length) => _length = length;

        public bool ReleaseOwnership(out byte[]? ownedBuffer, out int start, out int length)
        {
            ownedBuffer = buffer;
            start = 0;
            length = _length;
            return true;
        }

        public void Dispose()
        {
        }
    }

    private sealed record TestEndpoint(string Address) : INetworkEndpoint;
}
