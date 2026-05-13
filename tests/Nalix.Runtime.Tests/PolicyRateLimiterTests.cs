using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Primitives;
using Nalix.Network.Connections;
using Nalix.Runtime.Throttling;
using Xunit;

namespace Nalix.Runtime.Tests;

public sealed class PolicyRateLimiterTests : IDisposable
{
    private readonly PolicyRateLimiter _limiter;
    private static int s_opCodeCounter = 0x5000;
    private static int s_ipCounter = 1;

    private ushort NextOpCode() => (ushort)Interlocked.Increment(ref s_opCodeCounter);
    private string NextIp() => $"127.0.0.{Interlocked.Increment(ref s_ipCounter)}";

    public PolicyRateLimiterTests()
    {
        _limiter = new PolicyRateLimiter();
        
        // Isolate the test by using a fresh limiter engine instead of the shared singleton
        var field = typeof(PolicyRateLimiter).GetField("_shared", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(_limiter, new TokenBucketLimiter());
    }

    [Fact]
    public async Task Evaluate_SeparatePolicies_ShouldUseSeparateBuckets()
    {
        using var scope = await ConnectedSocketScope.CreateAsync(NextIp());
        using var connection = new Connection(scope.ServerSocket);

        ushort opA = NextOpCode();
        ushort opB = NextOpCode();

        var attrA = new PacketRateLimitAttribute(1, 1) { PolicyId = "PolicyA" };
        var contextA = new TestPacketContext(connection, opA, attrA);

        var attrB = new PacketRateLimitAttribute(1, 1) { PolicyId = "PolicyB" };
        var contextB = new TestPacketContext(connection, opB, attrB);

        _limiter.Evaluate(opA, contextA).Allowed.Should().BeTrue();
        _limiter.Evaluate(opB, contextB).Allowed.Should().BeTrue();

        _limiter.Evaluate(opA, contextA).Allowed.Should().BeFalse();
        _limiter.Evaluate(opB, contextB).Allowed.Should().BeFalse();
    }

    [Fact]
    public async Task Evaluate_SameIPDifferentOpCode_ShouldUseSeparateBuckets()
    {
        using var scope = await ConnectedSocketScope.CreateAsync(NextIp());
        using var connection = new Connection(scope.ServerSocket);

        ushort op1 = NextOpCode();
        ushort op2 = NextOpCode();

        var attr = new PacketRateLimitAttribute(1, 1);
        var context1 = new TestPacketContext(connection, op1, attr);
        var context2 = new TestPacketContext(connection, op2, attr);

        _limiter.Evaluate(op1, context1).Allowed.Should().BeTrue();
        _limiter.Evaluate(op2, context2).Allowed.Should().BeTrue();
        
        _limiter.Evaluate(op1, context1).Allowed.Should().BeFalse();
        _limiter.Evaluate(op2, context2).Allowed.Should().BeFalse();
    }

    [Fact]
    public async Task Evaluate_InvalidBurst_ShouldDenyRequest()
    {
        using var scope = await ConnectedSocketScope.CreateAsync(NextIp());
        using var connection = new Connection(scope.ServerSocket);

        ushort op = NextOpCode();
        var attr = new PacketRateLimitAttribute(10, 0); 
        var context = new TestPacketContext(connection, op, attr);

        var decision = _limiter.Evaluate(op, context);
        decision.Allowed.Should().BeFalse();
        decision.Reason.Should().Be(TokenBucketLimiter.RateLimitReason.HardLockout);
    }

    [Fact]
    public async Task Evaluate_NoRateLimitAttribute_ShouldAllowRequest()
    {
        using var scope = await ConnectedSocketScope.CreateAsync(NextIp());
        using var connection = new Connection(scope.ServerSocket);

        ushort op = NextOpCode();
        var context = new TestPacketContext(connection, op, null);

        var decision = _limiter.Evaluate(op, context);
        decision.Allowed.Should().BeTrue();
        decision.Reason.Should().Be(TokenBucketLimiter.RateLimitReason.None);
    }

    [Fact]
    public void GenerateReport_ShouldContainSharedEngineInfo()
    {
        string report = _limiter.GenerateReport();
        report.Should().Contain("PolicyRateLimiter Status");
        report.Should().Contain("Shared TokenBucket Engine Report");
    }

    public void Dispose()
    {
        _limiter.Dispose();
    }

    private sealed class TestPacketContext(IConnection connection, ushort opCode, PacketRateLimitAttribute? rateLimit) : IPacketContext<IPacket>
    {
        public bool IsReliable => true;
        public bool SkipOutbound => false;
        public IPacket Packet { get; } = new TestPacket(opCode);
        public IConnection Connection { get; } = connection;
        public PacketMetadata Attributes { get; } = new PacketMetadata(
                new PacketOpcodeAttribute(opCode),
                timeout: null,
                permission: null,
                encryption: null,
                rateLimit: rateLimit,
                concurrencyLimit: null,
                transport: null);

        public IPacketSender Sender => null!;
        public CancellationToken CancellationToken => CancellationToken.None;
        public void ResetForPool() { }
    }

    private sealed class TestPacket(ushort opCode) : IPacket
    {
        public int Length => 0;
        public PacketHeader Header { get; set; } = new PacketHeader { OpCode = opCode };
        public byte[] Serialize() => [];
        public int Serialize(Span<byte> buffer) => 0;
    }

    private sealed class ConnectedSocketScope : IDisposable
    {
        private ConnectedSocketScope(Socket listenerSocket, Socket clientSocket, Socket serverSocket)
        {
            ListenerSocket = listenerSocket;
            ClientSocket = clientSocket;
            ServerSocket = serverSocket;
        }

        public Socket ListenerSocket { get; }
        public Socket ClientSocket { get; }
        public Socket ServerSocket { get; }

        public static async Task<ConnectedSocketScope> CreateAsync(string address)
        {
            var ip = IPAddress.Loopback;
            Socket listener = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(new IPEndPoint(ip, 0));
            listener.Listen(1);

            int port = ((IPEndPoint)listener.LocalEndPoint!).Port;
            Task<Socket> acceptTask = Task.Run(() => listener.Accept());

            Socket client = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            await client.ConnectAsync(ip, port);

            Socket server = await acceptTask;
            return new ConnectedSocketScope(listener, client, server);
        }

        public void Dispose()
        {
            try { ClientSocket.Dispose(); } catch { }
            try { ServerSocket.Dispose(); } catch { }
            try { ListenerSocket.Dispose(); } catch { }
        }
    }
}
