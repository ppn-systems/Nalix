// TcpListenerHeavyBenchmark.cs

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Engines;
using Nalix.Common.Diagnostics.Abstractions;
using Nalix.Common.Networking.Abstractions;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Network.Abstractions;
using Nalix.Network.Configurations;
using Nalix.Network.Listeners.Tcp;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Nalix.Benchmark.Network.Network;

[MemoryDiagnoser]
[ThreadingDiagnoser]
[EventPipeProfiler(EventPipeProfile.CpuSampling)]
[SimpleJob(RunStrategy.ColdStart, iterationCount: 3, warmupCount: 1)]
public class TcpListenerHeavyBenchmark
{
    private class DummyProtocol : IProtocol
    {
        public Boolean KeepConnectionOpen => true;
        public void OnAccept(IConnection connection, CancellationToken cancellationToken) => connection.Close();
        public void ProcessMessage(Object sender, IConnectEventArgs args) { }
        public void PostProcessMessage(Object sender, IConnectEventArgs args) { }
        public String GenerateReport() => "DummyProtocol";
        public void Dispose() { }
    }

    private class SustainedProtocol : IProtocol
    {
        public Boolean KeepConnectionOpen => true;
        // Không close ngay — giữ connection sống để test sustained load
        public void OnAccept(IConnection connection, CancellationToken cancellationToken) { }
        public void ProcessMessage(Object sender, IConnectEventArgs args) { }
        public void PostProcessMessage(Object sender, IConnectEventArgs args) { }
        public String GenerateReport() => "SustainedProtocol";
        public void Dispose() { }
    }

    private class TestTcpListener : TcpListenerBase
    {
        public TestTcpListener(UInt16 port, IProtocol protocol) : base(port, protocol) { }
    }

    private TestTcpListener _listener;
    private IPEndPoint _endpoint;

    // ──────────────────────────────────────────────
    // Shared Setup / Cleanup
    // ──────────────────────────────────────────────

    private void InitListener(IProtocol protocol, Int32 preallocate = 1000)
    {
        InstanceManager.Instance.Register<ILogger>(new EmptyLogger());
        UInt16 freePort = GetFreePort();

        var socketOptions = ConfigurationManager.Instance.Get<NetworkSocketOptions>();
        socketOptions.Port = freePort;
        socketOptions.MaxParallel = Environment.ProcessorCount;
        socketOptions.EnableTimeout = false;
        socketOptions.BufferSize = 8192;
        socketOptions.Backlog = 20000;

        var poolOptions = ConfigurationManager.Instance.Get<PoolingOptions>();
        poolOptions.AcceptContextPreallocate = preallocate;
        poolOptions.AcceptContextMaxCapacity = preallocate * 20;
        poolOptions.SocketArgsPreallocate = preallocate;
        poolOptions.SocketArgsMaxCapacity = preallocate * 20;

        _listener = new TestTcpListener(freePort, protocol);
        _listener.Activate();
        _endpoint = new IPEndPoint(IPAddress.Loopback, freePort);
    }

    private void TeardownListener()
    {
        try { _listener?.Deactivate(); _listener?.Dispose(); }
        catch (ObjectDisposedException) { }
        finally { _listener = null; }
    }

    // ──────────────────────────────────────────────
    // 1. HIGH VOLUME — 10k connections/iteration
    // ──────────────────────────────────────────────

    [GlobalSetup(Target = nameof(HighVolume_10k))]
    public void Setup_HighVolume() => InitListener(new DummyProtocol(), preallocate: 10000);

    [GlobalCleanup(Target = nameof(HighVolume_10k))]
    public void Cleanup_HighVolume() => TeardownListener();

    [Benchmark(OperationsPerInvoke = 10000)]
    public void HighVolume_10k()
    {
        const Int32 count = 10000;
        var sockets = new Socket[count];

        for (Int32 i = 0; i < count; i++)
        {
            sockets[i] = new Socket(_endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            sockets[i].Connect(_endpoint);
        }

        Thread.Sleep(800);

        for (Int32 i = 0; i < count; i++)
        {
            try { sockets[i].Close(); sockets[i].Dispose(); }
            catch { }
        }

        Thread.Sleep(800);
    }

    // ──────────────────────────────────────────────
    // 2. CONCURRENT — nhiều thread connect đồng thời
    // ──────────────────────────────────────────────

    [GlobalSetup(Target = nameof(Concurrent_MultiThread))]
    public void Setup_Concurrent() => InitListener(new DummyProtocol(), preallocate: 5000);

    [GlobalCleanup(Target = nameof(Concurrent_MultiThread))]
    public void Cleanup_Concurrent() => TeardownListener();

    [Benchmark(OperationsPerInvoke = 5000)]
    public void Concurrent_MultiThread()
    {
        const Int32 total = 5000;
        const Int32 threads = 10;
        const Int32 perThread = total / threads;

        var sockets = new Socket[total];
        var tasks = new Task[threads];

        for (Int32 t = 0; t < threads; t++)
        {
            Int32 idx = t;
            tasks[t] = Task.Run(() =>
            {
                Int32 start = idx * perThread;
                for (Int32 i = start; i < start + perThread; i++)
                {
                    sockets[i] = new Socket(_endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    sockets[i].Connect(_endpoint);
                }
            });
        }

        Task.WaitAll(tasks);
        Thread.Sleep(600);

        for (Int32 i = 0; i < total; i++)
        {
            try { sockets[i]?.Close(); sockets[i]?.Dispose(); }
            catch { }
        }

        Thread.Sleep(600);
    }

    // ──────────────────────────────────────────────
    // 3. SUSTAINED LOAD — giữ connection sống 2 giây
    // ──────────────────────────────────────────────

    [GlobalSetup(Target = nameof(SustainedLoad_HeldConnections))]
    public void Setup_Sustained() => InitListener(new SustainedProtocol(), preallocate: 2000);

    [GlobalCleanup(Target = nameof(SustainedLoad_HeldConnections))]
    public void Cleanup_Sustained() => TeardownListener();

    [Benchmark(OperationsPerInvoke = 2000)]
    public void SustainedLoad_HeldConnections()
    {
        const Int32 count = 2000;
        var sockets = new Socket[count];

        for (Int32 i = 0; i < count; i++)
        {
            sockets[i] = new Socket(_endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            sockets[i].Connect(_endpoint);
        }

        // Giữ connections sống — đây là áp lực thực tế lên memory và thread pool
        Thread.Sleep(2000);

        for (Int32 i = 0; i < count; i++)
        {
            try { sockets[i].Close(); sockets[i].Dispose(); }
            catch { }
        }

        Thread.Sleep(500);
    }

    // ──────────────────────────────────────────────
    // 4. RAPID RECONNECT — không sleep giữa các lần
    // ──────────────────────────────────────────────

    [GlobalSetup(Target = nameof(RapidReconnect_NoSleep))]
    public void Setup_RapidReconnect() => InitListener(new DummyProtocol(), preallocate: 1000);

    [GlobalCleanup(Target = nameof(RapidReconnect_NoSleep))]
    public void Cleanup_RapidReconnect() => TeardownListener();

    [Benchmark(OperationsPerInvoke = 1000)]
    public void RapidReconnect_NoSleep()
    {
        // Connect rồi disconnect ngay lập tức, lặp liên tục
        // Mục tiêu: tìm race condition hoặc resource exhaustion ở tốc độ cao nhất
        for (Int32 i = 0; i < 1000; i++)
        {
            using var s = new Socket(_endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            s.Connect(_endpoint);
            s.Close();
        }
    }

    [GlobalSetup(Target = nameof(Concurrent_100k))]
    public void Setup_Concurrent100k()
    {
        InstanceManager.Instance.Register<ILogger>(new EmptyLogger());

        UInt16 freePort = GetFreePort();

        var socketOptions = ConfigurationManager.Instance.Get<NetworkSocketOptions>();
        socketOptions.Port = freePort;
        socketOptions.MaxParallel = Environment.ProcessorCount;
        socketOptions.EnableTimeout = false;
        socketOptions.BufferSize = 8192;

        var poolOptions = ConfigurationManager.Instance.Get<PoolingOptions>();
        poolOptions.AcceptContextPreallocate = 100_000;
        poolOptions.AcceptContextMaxCapacity = 200_000;
        poolOptions.SocketArgsPreallocate = 100_000;
        poolOptions.SocketArgsMaxCapacity = 200_000;

        _listener = new TestTcpListener(freePort, new DummyProtocol());
        _listener.Activate();
        _endpoint = new IPEndPoint(IPAddress.Loopback, freePort);
    }

    [GlobalCleanup(Target = nameof(Concurrent_100k))]
    public void Cleanup_Concurrent100k() => TeardownListener();

    [Benchmark(OperationsPerInvoke = 100_000)]
    public void Concurrent_100k()
    {
        const Int32 total = 100_000;
        const Int32 threads = 20;          // 20 thread × 5000 conns mỗi thread
        const Int32 perThread = total / threads;

        var sockets = new Socket[total];
        var tasks = new Task[threads];

        for (Int32 t = 0; t < threads; t++)
        {
            Int32 idx = t;
            tasks[t] = Task.Run(() =>
            {
                Int32 start = idx * perThread;
                for (Int32 i = start; i < start + perThread; i++)
                {
                    try
                    {
                        sockets[i] = new Socket(_endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                        sockets[i].Connect(_endpoint);
                    }
                    catch (SocketException ex)
                    {
                        // Log để biết bao nhiêu connection thực sự thành công
                        Console.WriteLine($"[{i}] SocketException: {ex.SocketErrorCode}");
                    }
                }
            });
        }

        Task.WaitAll(tasks);
        Thread.Sleep(2000);   // Server cần thời gian xử lý 100k accept events

        for (Int32 i = 0; i < total; i++)
        {
            try { sockets[i]?.Close(); sockets[i]?.Dispose(); }
            catch { }
        }

        Thread.Sleep(1000);
    }

    private static UInt16 GetFreePort()
    {
        using Socket socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return (UInt16)((IPEndPoint)socket.LocalEndPoint).Port;
    }
}