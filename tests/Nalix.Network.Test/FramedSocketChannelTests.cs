using Nalix.Network.Connection;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Nalix.Network.Tests;

public sealed class FramedSocketChannelTests
{
    private static async Task<(Socket server, Socket client)> CreateConnectedPairAsync()
    {
        var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        listener.Listen(1);
        var ep = (IPEndPoint)listener.LocalEndPoint!;

        var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        Task connect = client.ConnectAsync(ep.Address, ep.Port);
        Socket server = await listener.AcceptAsync().ConfigureAwait(false);
        await connect.ConfigureAwait(false);

        listener.Close();
        listener.Dispose();
        return (server, client);
    }

    [Fact]
    public async Task Client_FIN_Close_Triggers_OnClose_And_Hub_Unregister()
    {
        var (serverSock, clientSock) = await CreateConnectedPairAsync();

        var connection = new Connection.Connection(serverSock); // ctor wires channel callbacks
        var hub = new ConnectionHub();
        Assert.True(hub.RegisterConnection(connection));

        using var closed = new ManualResetEventSlim(false);
        connection.OnCloseEvent += (_, __) => closed.Set();

        // Start receive loop
        connection.TCP.BeginReceive();

        // graceful FIN close from client
        clientSock.Shutdown(SocketShutdown.Both);
        clientSock.Close();

        // Wait for server to detect and raise OnClose
        Assert.True(closed.Wait(TimeSpan.FromSeconds(3)), "OnCloseEvent was not fired within timeout.");

        // Hub should auto-unregister via its OnClose handler
        // Give a tiny breath for handler scheduling
        await Task.Delay(50);
        Assert.Equal(0, hub.ConnectionCount);

        // Cleanup
        connection.Dispose();
    }

    [Fact]
    public async Task Client_RST_Close_Triggers_OnClose_And_Hub_Unregister()
    {
        var (serverSock, clientSock) = await CreateConnectedPairAsync();

        var connection = new Connection.Connection(serverSock);
        var hub = new ConnectionHub();
        Assert.True(hub.RegisterConnection(connection));

        using var closed = new ManualResetEventSlim(false);
        connection.OnCloseEvent += (_, __) => closed.Set();

        connection.TCP.BeginReceive();

        // Abrupt close (RST): Linger enabled with 0 seconds
        clientSock.LingerState = new LingerOption(true, 0);
        clientSock.Close(); // sends RST

        Assert.True(closed.Wait(TimeSpan.FromSeconds(3)), "OnCloseEvent was not fired within timeout.");
        await Task.Delay(50);
        Assert.Equal(0, hub.ConnectionCount);

        connection.Dispose();
    }

    [Fact]
    public async Task Incoming_Frame_Is_Pushed_To_Incoming_Cache()
    {
        var (serverSock, clientSock) = await CreateConnectedPairAsync();
        var connection = new Connection.Connection(serverSock);

        // Start receive loop
        connection.TCP.BeginReceive();

        // Waiter for "packet arrived"
        using var arrived = new ManualResetEventSlim(false);
        connection.OnProcessEvent += (_, __) => arrived.Set();

        // Build one framed message: [len(2)][payload]
        Byte[] payload = System.Text.Encoding.UTF8.GetBytes("hello");
        UInt16 total = (UInt16)(payload.Length + 2);
        Byte[] framed = new Byte[total];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(framed.AsSpan(0, 2), total);
        payload.CopyTo(framed.AsSpan(2));

        // Send from client
        _ = clientSock.Send(framed);

        // Wait until BufferLeaseCache.PushIncoming() has fired the event
        Assert.True(arrived.Wait(TimeSpan.FromSeconds(2)), "Did not receive OnProcessEvent in time.");

        // Now it's safe to Pop exactly once
        var lease = connection.IncomingPacket; // Pop() now has data
        Assert.NotNull(lease);
        Assert.Equal(payload.Length, lease!.Length);
        Span<Byte> buf = stackalloc Byte[payload.Length];
        lease.Span.CopyTo(buf);
        Assert.True(buf.SequenceEqual(payload));

        // Cleanup
        clientSock.Close();
        connection.Dispose();
    }
}
