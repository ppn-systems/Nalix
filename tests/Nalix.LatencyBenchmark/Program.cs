using System.Diagnostics;
using System.Collections.Concurrent;
using Nalix.SDK.Options;
using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Extensions;
using Nalix.Framework.DataFrames;
using Nalix.Framework.DataFrames.SignalFrames;

// 0. Set process priority to high for more stable results
Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;

Console.WriteLine("=== Nalix SDK Latency Test (Optimized Benchmark) ===");

// 1. Setup Registry and Options
var registry = new PacketRegistry(factory => 
{
    factory.RegisterPacket<Control>();
});

const string Host = "127.0.0.1";
const ushort Port = 57206;
const int SessionCount = 500;
const int PingsPerSession = 10000;
const int TotalIterations = SessionCount * PingsPerSession;

var clientOptions = new TransportOptions 
{ 
    NoDelay = true 
};

// 2. Connect Multiple Sessions
Console.WriteLine($"Connecting {SessionCount} sessions to {Host}:{Port}...");
var sessions = new TcpSession[SessionCount];
try
{
    var connectTasks = new Task[SessionCount];
    for (int i = 0; i < SessionCount; i++)
    {
        var session = new TcpSession(clientOptions, registry);
        sessions[i] = session;
        connectTasks[i] = session.ConnectAsync(Host, Port);
        
        if (i % 50 == 0) await Task.Delay(10).ConfigureAwait(false);
    }
    await Task.WhenAll(connectTasks).ConfigureAwait(false);
    Console.WriteLine($"All {SessionCount} sessions connected.");
}
catch (Exception ex)
{
    Console.WriteLine($"Connection failed: {ex.Message}");
    Console.WriteLine($"Ensure the Nalix Server is running on port {Port}.");
    return;
}

try
{
    // 3. Warm-up
    const int WarmupIterations = 200;
    Console.WriteLine($"\nWarming up ({WarmupIterations} pings per session to stabilize buffers)...");
    await Parallel.ForEachAsync(sessions, async (session, ct) =>
    {
        for (int i = 0; i < WarmupIterations; i++)
        {
            await session.PingAsync(ct: ct).ConfigureAwait(false);
        }
    }).ConfigureAwait(false);

    // 4. Force GC and wait to ensure a clean state
    Console.WriteLine("Performing GC cleanup before benchmarking...");
    GC.Collect(2, GCCollectionMode.Forced, true);
    GC.WaitForPendingFinalizers();
    GC.Collect(2, GCCollectionMode.Forced, true);

    // 5. Benchmark
    Console.WriteLine($"Benchmarking RTT ({TotalIterations} total pings, {PingsPerSession} per session)...");

    var samples = new double[TotalIterations];
    int completedPings = 0;
    var sw = Stopwatch.StartNew();

    var tasks = new Task[SessionCount];
    for (int i = 0; i < SessionCount; i++)
    {
        int sessionId = i;
        tasks[i] = Task.Run(async () => 
        {
            var session = sessions[sessionId];
            for (int j = 0; j < PingsPerSession; j++)
            {
                try
                {
                    double rtt = await session.PingAsync().ConfigureAwait(false);
                    int index = Interlocked.Increment(ref completedPings) - 1;
                    if (index < TotalIterations)
                    {
                        samples[index] = rtt;
                    }

                    if (completedPings % 5000 == 0)
                    {
                        Console.WriteLine($"  -> Progress: {completedPings}/{TotalIterations}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ping failed on session {sessionId}: {ex.Message}");
                }
            }
        });
    }

    await Task.WhenAll(tasks).ConfigureAwait(false);
    sw.Stop();

    // 6. Statistics Calculation
    var sampleList = samples.Where(x => x > 0).ToList();
    sampleList.Sort();

    int count = sampleList.Count;
    if (count == 0)
    {
        Console.WriteLine("No samples collected. Benchmark failed.");
        return;
    }

    double avg = sampleList.Average();
    double median = sampleList[count / 2];
    double p90 = sampleList[(int)(count * 0.90)];
    double p95 = sampleList[(int)(count * 0.95)];
    double p99 = sampleList[(int)(count * 0.99)];
    double p999 = sampleList[(int)(count * 0.999)];
    double p9999 = sampleList[(int)(count * 0.9999)];
    double min = sampleList[0];
    double max = sampleList[^1];

    double sumOfSquares = sampleList.Select(val => (val - avg) * (val - avg)).Sum();
    double stdDev = Math.Sqrt(sumOfSquares / count);

    double totalSeconds = sw.Elapsed.TotalSeconds;
    double tps = count / totalSeconds;

    Console.WriteLine("\n" + new string('=', 40));
    Console.WriteLine("       DETAILED LATENCY STATISTICS       ");
    Console.WriteLine(new string('=', 40));

    Console.WriteLine($"{"Metric",-15} | {"ms",-10} | {"us",-10}");
    Console.WriteLine(new string('-', 40));
    Console.WriteLine($"{"Minimum",-15} | {min,10:F4} | {min * 1000,10:F0}");
    Console.WriteLine($"{"Average",-15} | {avg,10:F4} | {avg * 1000,10:F0}");
    Console.WriteLine($"{"Median (P50)",-15} | {median,10:F4} | {median * 1000,10:F0}");
    Console.WriteLine($"{"90th Pctl",-15} | {p90,10:F4} | {p90 * 1000,10:F0}");
    Console.WriteLine($"{"95th Pctl",-15} | {p95,10:F4} | {p95 * 1000,10:F0}");
    Console.WriteLine($"{"99th Pctl",-15} | {p99,10:F4} | {p99 * 1000,10:F0}");
    Console.WriteLine($"{"99.9th Pctl",-15} | {p999,10:F4} | {p999 * 1000,10:F0}");
    Console.WriteLine($"{"99.99th Pctl",-15} | {p9999,10:F4} | {p9999 * 1000,10:F0}");
    Console.WriteLine($"{"Maximum",-15} | {max,10:F4} | {max * 1000,10:F0}");
    Console.WriteLine(new string('-', 40));
    Console.WriteLine($"{"Std Deviation",-15} | {stdDev,10:F4} | {stdDev * 1000,10:F0} (Jitter)");
    Console.WriteLine($"{"Throughput",-15} | {tps,10:F0} | ops/sec");
    Console.WriteLine(new string('=', 40));
}
finally
{
    foreach (var s in sessions) s?.Dispose();
}

Console.WriteLine("\nBenchmark completed. Press Enter to exit...");

if (!Console.IsInputRedirected)
{
    Console.ReadLine();
}



