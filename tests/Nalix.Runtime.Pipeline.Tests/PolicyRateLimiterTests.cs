using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Network.Connections;
using Nalix.Runtime.Options;
using Nalix.Runtime.Throttling;
using Xunit;

namespace Nalix.Runtime.Tests;

[SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "xUnit tests intentionally follow the test synchronization context.")]
public sealed class PolicyRateLimiterTests
{
    private static readonly FieldInfo s_limitersField =
        typeof(PolicyRateLimiter).GetField("_limiters", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("PolicyRateLimiter._limiters field was not found.");

    private static readonly MethodInfo s_evictStalePoliciesMethod =
        typeof(PolicyRateLimiter).GetMethod("EVICT_STALE_POLICIES", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("PolicyRateLimiter.EVICT_STALE_POLICIES method was not found.");

    [Fact]
    public async Task Evaluate_UsesSeparateBucketsPerOpcode()
    {
        using PolicyRateLimiter limiter = new();
        using ConnectedSocketScope scope = await ConnectedSocketScope.CreateAsync();
        using Connection connection = new(scope.ServerSocket);

        PacketRateLimitAttribute rateLimit = new(requestsPerSecond: 1, burst: 1);
        TestPacketContext contextA = new(connection, opCode: 0x1000, rateLimit);
        TestPacketContext contextB = new(connection, opCode: 0x1001, rateLimit);

        TokenBucketLimiter.RateLimitDecision firstA = limiter.Evaluate(0x1000, contextA);
        TokenBucketLimiter.RateLimitDecision firstB = limiter.Evaluate(0x1001, contextB);
        TokenBucketLimiter.RateLimitDecision secondA = limiter.Evaluate(0x1000, contextA);

        firstA.Allowed.Should().BeTrue();
        firstB.Allowed.Should().BeTrue();
        secondA.Allowed.Should().BeFalse();
    }

    [Fact]
    public async Task Evaluate_StalePolicyEntry_IsEvictedBySweep()
    {
        using PolicyRateLimiter limiter = new();
        using ConnectedSocketScope scope = await ConnectedSocketScope.CreateAsync();
        using Connection connection = new(scope.ServerSocket);

        TestPacketContext context = new(connection, opCode: 0x2000, new PacketRateLimitAttribute(4, 1));
        _ = limiter.Evaluate(0x2000, context);

        object limiters = s_limitersField.GetValue(limiter)!;
        PropertyInfo countProperty = limiters.GetType().GetProperty("Count")
            ?? throw new InvalidOperationException("PolicyRateLimiter._limiters Count property was not found.");
        PropertyInfo valuesProperty = limiters.GetType().GetProperty("Values")
            ?? throw new InvalidOperationException("PolicyRateLimiter._limiters Values property was not found.");

        countProperty.GetValue(limiters).Should().Be(1);

        IEnumerable values = (IEnumerable)valuesProperty.GetValue(limiters)!;
        object entry = values.Cast<object>().Single();
        FieldInfo lastUsedTicksField = entry.GetType().GetField("_lastUsedUtcTicks", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("PolicyRateLimiter.Entry._lastUsedUtcTicks field was not found.");
        lastUsedTicksField.SetValue(entry, DateTime.UtcNow.AddHours(-1).Ticks);

        _ = s_evictStalePoliciesMethod.Invoke(limiter, parameters: null);

        countProperty.GetValue(limiters).Should().Be(0);
    }

    private sealed class TestPacketContext(Connection connection, ushort opCode, PacketRateLimitAttribute rateLimit) : IPacketContext<IPacket>
    {
        public bool IsReliable => true;

        public bool SkipOutbound => false;

        public IPacket Packet { get; } = new TestPacket(opCode);

        public IConnection Connection { get; } = connection;

        public PacketMetadata Attributes { get; } =
            new(new PacketOpcodeAttribute(opCode), timeout: null, permission: null, encryption: null, rateLimit, concurrencyLimit: null, transport: null);

        public IPacketSender Sender => throw new NotSupportedException();

        public CancellationToken CancellationToken => CancellationToken.None;

        public void ResetForPool()
        {
        }
    }

    private sealed class TestPacket(ushort opCode) : IPacket
    {
        public int Length => 0;

        public uint MagicNumber { get; set; }

        public ushort OpCode { get; set; } = opCode;

        public PacketFlags Flags { get; set; }

        public PacketPriority Priority { get; set; }

        public bool IsReliable { get; set; } = true;

        public ushort SequenceId { get; } = 1;

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

        public static async Task<ConnectedSocketScope> CreateAsync()
        {
            Socket listener = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            listener.Listen(1);

            int port = ((IPEndPoint)listener.LocalEndPoint!).Port;
            Task<Socket> acceptTask = Task.Run(() => listener.Accept());

            Socket client = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            await client.ConnectAsync(IPAddress.Loopback, port);

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
