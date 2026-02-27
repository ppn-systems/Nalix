using System;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using Nalix.Framework.DataFrames.Chunks;
using Nalix.Network.Connections;
using Xunit;
using TransportAsyncCallback = Nalix.Network.Internal.Transport.AsyncCallback;

namespace Nalix.Network.Tests;

[SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "xUnit tests intentionally follow the test synchronization context.")]
[Collection(AsyncCallbackSerialGroup.Name)]
public sealed class SocketConnectionFragmentationTests
{
    private static readonly FieldInfo s_fragmentAssemblerField =
        typeof(Nalix.Network.Internal.Transport.SocketConnection).GetField("_fragmentAssembler", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("SocketConnection._fragmentAssembler field was not found.");

    [Fact]
    public async Task BeginReceive_NonFragmentedFrame_DoesNotCreateFragmentAssembler()
    {
        using ConnectedSocketScope scope = await ConnectedSocketScope.CreateAsync();
        using Connection connection = new(scope.ServerSocket);
        TransportAsyncCallback.ResetStatistics();

        TaskCompletionSource<int> processObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.OnProcessEvent += (_, args) =>
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

        connection.Socket.BeginReceive();
        await scope.ClientSocket.SendAsync(CreateFrame([1, 2, 3, 4]));

        int receivedLength = await processObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));

        receivedLength.Should().Be(4);
        s_fragmentAssemblerField.GetValue(connection.Socket).Should().BeNull();
    }

    [Fact]
    public async Task BeginReceive_FragmentedFrame_CreatesFragmentAssemblerOnDemand()
    {
        using ConnectedSocketScope scope = await ConnectedSocketScope.CreateAsync();
        using Connection connection = new(scope.ServerSocket);
        TransportAsyncCallback.ResetStatistics();

        TaskCompletionSource<byte[]> processObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.OnProcessEvent += (_, args) =>
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

        connection.Socket.BeginReceive();
        await scope.ClientSocket.SendAsync(CreateFrame(CreateFragmentPayload([9, 8, 7])));

        byte[] receivedPayload = await processObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));

        receivedPayload.Should().Equal([9, 8, 7]);
        s_fragmentAssemblerField.GetValue(connection.Socket).Should().NotBeNull();
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
