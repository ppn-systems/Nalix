using Nalix.Codec.Memory;
#if DEBUG
using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Buffers;
using Nalix.Network.Connections;
using Xunit;
using TransportAsyncCallback = Nalix.Network.Internal.Transport.AsyncCallback;

namespace Nalix.Network.Tests;

[SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "xUnit tests intentionally follow the test synchronization context.")]
[Collection(AsyncCallbackSerialGroup.Name)]
public sealed class AsyncCallbackDispatchTests
{
    [Fact]
    public async Task InjectIncoming_QueuesProcessCallbackOnce_AndDoesNotUnderflowPendingPackets()
    {
        EnsureLoggerRegistered();

        using Socket listener = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        listener.Listen(1);

        int port = ((IPEndPoint)listener.LocalEndPoint!).Port;

        Task<Socket> acceptTask = Task.Run(() => listener.Accept());

        using Socket clientSocket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        await clientSocket.ConnectAsync(IPAddress.Loopback, port);

        using Socket serverSocket = await acceptTask;
        using Connection connection = new(serverSocket);

        TaskCompletionSource processObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.OnProcessEvent += (_, e) =>
        {
            if (e is null)
            {
                _ = processObserved.TrySetException(new InvalidOperationException("Process callback args were null."));
                return;
            }

            _ = e.Lease.Should().NotBeNull();
            _ = e.Lease!.Length.Should().Be(3);
            _ = processObserved.TrySetResult();
        };

        TransportAsyncCallback.ResetStatistics();

        BufferLease lease = BufferLease.CopyFrom([1, 2, 3]);
        connection.InjectIncoming(lease);

        await processObserved.Task.WaitAsync(TimeSpan.FromSeconds(15));

        // Wait for the dispatcher's finally block to release the pending packet slot.
        for (int i = 0; i < 100 && connection.Socket.PendingPackets > 0; i++)
        {
            await Task.Delay(1);
        }

        (int pendingNormal, long dropped, long total) = TransportAsyncCallback.GetStatistics();
        _ = pendingNormal.Should().Be(0);
        _ = dropped.Should().Be(0);
        _ = total.Should().Be(1);
        _ = connection.Socket.PendingPackets.Should().Be(0);
    }

    [Fact]
    public async Task Send_PostProcessCallback_DoesNotConsumeReceivePendingSlot()
    {
        EnsureLoggerRegistered();

        using Socket listener = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        listener.Listen(1);

        int port = ((IPEndPoint)listener.LocalEndPoint!).Port;

        Task<Socket> acceptTask = Task.Run(() => listener.Accept());

        using Socket clientSocket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        await clientSocket.ConnectAsync(IPAddress.Loopback, port);

        using Socket serverSocket = await acceptTask;
        using Connection connection = new(serverSocket);

        TaskCompletionSource postObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.OnPostProcessEvent += (_, _) => postObserved.TrySetResult();

        TransportAsyncCallback.ResetStatistics();

        connection.TCP.Send([7, 8, 9, 10]);

        byte[] receivedFrame = new byte[32];
        int bytesRead = await clientSocket.ReceiveAsync(receivedFrame, SocketFlags.None);
        _ = bytesRead.Should().BeGreaterThan(0);

        await postObserved.Task.WaitAsync(TimeSpan.FromSeconds(15));

        // Wait for the dispatcher's finally block to release the pending packet slot.
        for (int i = 0; i < 100 && connection.Socket.PendingPackets > 0; i++)
        {
            await Task.Delay(1);
        }

        (int pendingNormal, long dropped, long total) = TransportAsyncCallback.GetStatistics();
        _ = pendingNormal.Should().Be(0);
        _ = dropped.Should().Be(0);
        _ = total.Should().Be(1);
        _ = connection.Socket.PendingPackets.Should().Be(0);
    }

    private static void EnsureLoggerRegistered()
    {
        _ = InstanceManager.Instance.WithLogging(NullLogger.Instance);
        InstanceManager.Instance.Register<ILogger>(NullLogger.Instance);
    }
}
#endif














