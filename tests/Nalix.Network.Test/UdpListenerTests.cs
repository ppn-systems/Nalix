using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.Identifiers;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Buffers;
using Nalix.Network.Connections;
using Nalix.Network.Listeners.Udp;
using Nalix.Network.Protocols;
using Xunit;

namespace Nalix.Network.Tests;

[SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "xUnit tests intentionally follow the test synchronization context.")]
public sealed class UdpListenerTests
{
    private static readonly ConnectionHub s_hub = InitializeUdpListenerStatics();

    [Fact]
    public void ProcessDatagram_ShortPacket_IncrementsDropShort()
    {
        EnsureUdpListenerStatics();

        using TestUdpListener listener = new(new CountingProtocol());

        listener.Process(BufferLease.CopyFrom([1, 2, 3]), new IPEndPoint(IPAddress.Loopback, 12345));

        GetTrafficReport(listener)["DroppedShort"].Should().Be(1L);
        GetTrafficReport(listener)["DroppedUnknown"].Should().Be(0L);
    }

    [Fact]
    public void ProcessDatagram_InvalidTransport_IncrementsDropShort()
    {
        EnsureUdpListenerStatics();

        using TestUdpListener listener = new(new CountingProtocol());
        byte[] datagram = CreateDatagram(new byte[Snowflake.Size], reliable: true);

        listener.Process(BufferLease.CopyFrom(datagram), new IPEndPoint(IPAddress.Loopback, 22345));

        GetTrafficReport(listener)["DroppedShort"].Should().Be(1L);
        GetTrafficReport(listener)["DroppedUnknown"].Should().Be(0L);
    }

    [Fact]
    public void ProcessDatagram_UnknownSessionToken_IncrementsDropUnknown()
    {
        EnsureUdpListenerStatics();

        using TestUdpListener listener = new(new CountingProtocol());
        byte[] datagram = CreateDatagram(
            Snowflake.NewId(Nalix.Common.Identity.SnowflakeType.Session).ToByteArray(),
            reliable: false);

        listener.Process(BufferLease.CopyFrom(datagram), new IPEndPoint(IPAddress.Loopback, 32345));

        GetTrafficReport(listener)["DroppedUnknown"].Should().Be(1L);
    }


    private static ConnectionHub EnsureUdpListenerStatics()
        => s_hub;

    private static ConnectionHub InitializeUdpListenerStatics()
    {
        _ = InstanceManager.Instance.WithLogging(NullLogger.Instance);
        InstanceManager.Instance.Register<ILogger>(NullLogger.Instance);

        if (InstanceManager.Instance.GetExistingInstance<IConnectionHub>() is ConnectionHub existingHub)
        {
            return existingHub;
        }

        ConnectionHub hub = new();
        InstanceManager.Instance.Register<IConnectionHub>(hub, registerInterfaces: false);
        InstanceManager.Instance.Register(hub);

        return hub;
    }

    private static Dictionary<string, object> GetTrafficReport(TestUdpListener listener)
        => (Dictionary<string, object>)listener.GetReportData()["Traffic"];

    private static Dictionary<string, object> GetRuntimeReport(TestUdpListener listener)
        => (Dictionary<string, object>)listener.GetReportData()["Runtime"];

    private static byte[] CreateDatagram(byte[] sessionToken, bool reliable)
    {
        // SessionToken(8) + Header(10)
        byte[] datagram = new byte[Snowflake.Size + 10];
        sessionToken.CopyTo(datagram, 0);
        
        if (!reliable)
        {
            // Set UNRELIABLE bit (0x02) at offset 6 relative to packet start (index 14 total)
            datagram[Snowflake.Size + 6] |= (byte)PacketFlags.UNRELIABLE;
        }
        else
        {
            // Set RELIABLE bit (0x01)
            datagram[Snowflake.Size + 6] |= (byte)PacketFlags.RELIABLE;
        }
        return datagram;
    }

    private sealed class TestUdpListener(IProtocol protocol) : UdpListenerBase(0, protocol, s_hub)
    {
        public void Process(BufferLease lease, EndPoint remoteEndPoint) => this.ProcessDatagram(lease, remoteEndPoint);

        protected override bool IsAuthenticated(IConnection connection, EndPoint remoteEndPoint, ReadOnlySpan<byte> payload) => true;
    }

    private sealed class CountingProtocol : Protocol
    {
        public int ProcessedFrames => Volatile.Read(ref _processedFrames);

        private int _processedFrames;

        public override void ProcessMessage(object? sender, IConnectEventArgs args)
        {
            _ = Interlocked.Increment(ref _processedFrames);
            args.Lease?.Dispose();
        }
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
            Socket listener = new(AddressFamily.InterNetwork, SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
            listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            listener.Listen(1);

            int port = ((IPEndPoint)listener.LocalEndPoint!).Port;
            Task<Socket> acceptTask = Task.Run(() => listener.Accept());

            Socket client = new(AddressFamily.InterNetwork, SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
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
