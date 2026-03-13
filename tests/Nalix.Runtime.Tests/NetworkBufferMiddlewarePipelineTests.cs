using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Abstractions;
using Nalix.Common.Exceptions;
using Nalix.Common.Middleware;
using Nalix.Common.Networking;
using Nalix.Framework.Memory.Buffers;
using Nalix.Network.Connections;
using Nalix.Runtime.Middleware;
using Xunit;

namespace Nalix.Runtime.Tests;

[SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "xUnit tests intentionally follow the test synchronization context.")]
public sealed class NetworkBufferMiddlewarePipelineTests
{
    [Fact]
    public async Task ExecuteAsync_WhenConnectionIsNull_DisposesLeaseAndReturnsNull()
    {
        NetworkBufferMiddlewarePipeline pipeline = new();
        BufferLease lease = BufferLease.CopyFrom([1, 2, 3]);

        IBufferLease? result = await pipeline.ExecuteAsync(lease, null!, CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, lease.Capacity);
    }

    [Fact]
    public async Task ExecuteAsync_WhenLeaseIsNull_ReturnsNullWithoutThrowing()
    {
        NetworkBufferMiddlewarePipeline pipeline = new();

        IBufferLease? result = await pipeline.ExecuteAsync(null!, null!, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public void Use_WhenSameMiddlewareInstanceRegisteredTwice_ThrowsInternalErrorException()
    {
        NetworkBufferMiddlewarePipeline pipeline = new();
        RecordingMiddleware middleware = new(_ => ValueTask.FromResult<IBufferLease?>(null));

        pipeline.Use(middleware);

        _ = Assert.Throws<InternalErrorException>(() => pipeline.Use(middleware));
    }

    [Fact]
    public async Task ExecuteAsync_WhenMiddlewaresHaveOrder_ExecutesInAscendingOrder()
    {
        NetworkBufferMiddlewarePipeline pipeline = new();
        List<int> trace = [];
        pipeline.Use(new OrderTwoMiddleware(trace));
        pipeline.Use(new OrderMinusOneMiddleware(trace));

        using ConnectedSocketScope scope = await ConnectedSocketScope.CreateAsync();
        using Connection connection = new(scope.ServerSocket);
        using BufferLease lease = BufferLease.CopyFrom([0x10]);

        IBufferLease? result = await pipeline.ExecuteAsync(lease, connection, CancellationToken.None);

        Assert.Same(lease, result);
        Assert.Equal([ -1, 2 ], trace);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMiddlewareReturnsReplacement_DisposesPreviousLease()
    {
        NetworkBufferMiddlewarePipeline pipeline = new();
        pipeline.Use(new RecordingMiddleware(_ => ValueTask.FromResult<IBufferLease?>(BufferLease.CopyFrom([9, 9, 9]))));

        using ConnectedSocketScope scope = await ConnectedSocketScope.CreateAsync();
        using Connection connection = new(scope.ServerSocket);
        BufferLease original = BufferLease.CopyFrom([1, 2, 3]);

        IBufferLease? result = await pipeline.ExecuteAsync(original, connection, CancellationToken.None);

        Assert.NotNull(result);
        Assert.NotSame(original, result);
        Assert.Equal(0, original.Capacity);
        Assert.Equal([9, 9, 9], result!.Memory.ToArray());
        result.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_WhenMiddlewareReturnsNull_DropsPacketAndDisposesLease()
    {
        NetworkBufferMiddlewarePipeline pipeline = new();
        pipeline.Use(new RecordingMiddleware(_ => ValueTask.FromResult<IBufferLease?>(null)));

        using ConnectedSocketScope scope = await ConnectedSocketScope.CreateAsync();
        using Connection connection = new(scope.ServerSocket);
        BufferLease original = BufferLease.CopyFrom([1, 2, 3, 4]);

        IBufferLease? result = await pipeline.ExecuteAsync(original, connection, CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, original.Capacity);
    }

    [Fact]
    public async Task Clear_WhenCalledAfterUse_RemovesAllRegisteredMiddleware()
    {
        NetworkBufferMiddlewarePipeline pipeline = new();
        int invoked = 0;
        pipeline.Use(new RecordingMiddleware(buffer =>
        {
            invoked++;
            return ValueTask.FromResult<IBufferLease?>(buffer);
        }));
        pipeline.Clear();

        using ConnectedSocketScope scope = await ConnectedSocketScope.CreateAsync();
        using Connection connection = new(scope.ServerSocket);
        using BufferLease lease = BufferLease.CopyFrom([0xAA]);

        IBufferLease? result = await pipeline.ExecuteAsync(lease, connection, CancellationToken.None);

        Assert.Same(lease, result);
        Assert.Equal(0, invoked);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMiddlewareIsAsync_CompletesWithoutDisposingCurrentLease()
    {
        NetworkBufferMiddlewarePipeline pipeline = new();
        pipeline.Use(new RecordingMiddleware(async buffer =>
        {
            await Task.Yield();
            return buffer;
        }));

        using ConnectedSocketScope scope = await ConnectedSocketScope.CreateAsync();
        using Connection connection = new(scope.ServerSocket);
        using BufferLease lease = BufferLease.CopyFrom([5, 6, 7]);

        IBufferLease? result = await pipeline.ExecuteAsync(lease, connection, CancellationToken.None);

        Assert.Same(lease, result);
        Assert.True(lease.Capacity > 0);
    }

    [MiddlewareOrder(2)]
    private sealed class OrderTwoMiddleware(List<int> trace) : INetworkBufferMiddleware
    {
        public ValueTask<IBufferLease?> InvokeAsync(IBufferLease buffer, IConnection connection, CancellationToken ct)
        {
            trace.Add(2);
            return ValueTask.FromResult<IBufferLease?>(buffer);
        }
    }

    [MiddlewareOrder(-1)]
    private sealed class OrderMinusOneMiddleware(List<int> trace) : INetworkBufferMiddleware
    {
        public ValueTask<IBufferLease?> InvokeAsync(IBufferLease buffer, IConnection connection, CancellationToken ct)
        {
            trace.Add(-1);
            return ValueTask.FromResult<IBufferLease?>(buffer);
        }
    }

    private sealed class RecordingMiddleware(Func<IBufferLease, ValueTask<IBufferLease?>> run) : INetworkBufferMiddleware
    {
        public ValueTask<IBufferLease?> InvokeAsync(IBufferLease buffer, IConnection connection, CancellationToken ct) => run(buffer);
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
