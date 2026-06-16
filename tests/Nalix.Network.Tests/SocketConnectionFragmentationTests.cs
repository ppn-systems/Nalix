#if DEBUG
using System;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Environment.Fragments;
using Nalix.Network.Connections;
using Nalix.Network.Internal.Transport;
using Xunit;
using TransportAsyncCallback = Nalix.Network.Internal.Transport.AsyncCallback;

namespace Nalix.Network.Tests;

[SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "xUnit tests intentionally follow the test synchronization context.")]
[Collection(AsyncCallbackSerialGroup.Name)]
public sealed class SocketConnectionFragmentationTests
{
    private static readonly IOpCodeExtractor s_testOpCodeExtractor = new TestOpCodeExtractor();

    private static readonly FieldInfo s_backingField =
        typeof(Connection).GetField("_backing", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Connection._backing field was not found.");

    private static readonly FieldInfo s_backingSocketField =
        typeof(Nalix.Network.Internal.Connections.ConnectionBacking).GetField("Socket", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("ConnectionBacking.Socket field was not found.");

    private static readonly FieldInfo s_fragmentAssemblerField =
        typeof(Nalix.Network.Internal.Transport.SocketConnection).GetField("_fragmentAssembler", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("SocketConnection._fragmentAssembler field was not found.");

    private static object? GetFragmentAssembler(Connection connection)
    {
        object? backing = s_backingField.GetValue(connection);
        if (backing == null) return null;
        object? socketConnection = s_backingSocketField.GetValue(backing);
        return socketConnection is null ? null : s_fragmentAssemblerField.GetValue(socketConnection);
    }

    private sealed class TestOpCodeExtractor : IOpCodeExtractor
    {
        public ushort Extract(System.ReadOnlySpan<byte> payload) =>
            payload.Length >= 2 ? System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(payload[0..]) : (ushort)0;
    }

    [Fact]
    public async Task BeginReceive_NonFragmentedFrame_DoesNotCreateFragmentAssembler()
    {
        using ConnectedSocketScope scope = await ConnectedSocketScope.CreateAsync();
        using Connection connection = new(scope.ServerSocket, s_testOpCodeExtractor);
        TransportAsyncCallback.ResetStatistics();

        TaskCompletionSource<int> processObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.MessageProcessing += (_, args) =>
        {
            try
            {
                processObserved.TrySetResult(args.Lease?.Length ?? -1);
            }
            finally
            {
                args.Lease?.Dispose();
            }
        };

        connection.TCP.BeginReceive();
        await scope.ClientSocket.SendAsync(CreateFrame([1, 2, 3, 4, 5, 6, 7, 8, 9, 10]));

        int receivedLength = await processObserved.Task.WaitAsync(TimeSpan.FromSeconds(15));

        receivedLength.Should().Be(10);
        GetFragmentAssembler(connection).Should().BeNull();
    }

    [Fact]
    public async Task BeginReceive_FragmentedFrame_CreatesFragmentAssemblerOnDemand()
    {
        using ConnectedSocketScope scope = await ConnectedSocketScope.CreateAsync();
        using Connection connection = new(scope.ServerSocket, s_testOpCodeExtractor);
        TransportAsyncCallback.ResetStatistics();

        TaskCompletionSource<byte[]> processObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.MessageProcessing += (_, args) =>
        {
            try
            {
                processObserved.TrySetResult(args.Lease?.Span.ToArray() ?? []);
            }
            finally
            {
                args.Lease?.Dispose();
            }
        };

        connection.TCP.BeginReceive();
        await scope.ClientSocket.SendAsync(CreateFrame(CreateFragmentPayload([9, 8, 7])));

        byte[] receivedPayload = await processObserved.Task.WaitAsync(TimeSpan.FromSeconds(15));

        receivedPayload.Should().Equal([9, 8, 7]);
        GetFragmentAssembler(connection).Should().NotBeNull();
    }

    private static byte[] CreateFrame(ReadOnlySpan<byte> payload)
    {
        byte[] frame = new byte[payload.Length + sizeof(ushort)];
        BinaryPrimitives.WriteUInt16LittleEndian(frame, (ushort)frame.Length);
        payload.CopyTo(frame.AsSpan(sizeof(ushort)));
        return frame;
    }

    private static byte[] CreateFragmentPayload(ReadOnlySpan<byte> body)
    {
        byte[] payload = new byte[FragmentHeader.WireSize + body.Length];
        FragmentHeader header = new(streamId: 1, chunkIndex: 0, totalChunks: 1, isLast: true);
        header.WriteTo(payload);
        body.CopyTo(payload.AsSpan(FragmentHeader.WireSize));
        return payload;
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
#endif














