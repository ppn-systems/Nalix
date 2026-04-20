using System.Diagnostics;
using Nalix.SDK.Options;
using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Extensions;
using Nalix.Framework.DataFrames;
using Nalix.Framework.DataFrames.SignalFrames;

Console.WriteLine("=== Nalix SDK Latency Test (Pure Client) ===");

// 1. Setup Registry and Client
// We need to register the Control packet so the SDK can recognize PONG responses.
var registry = new PacketRegistry(factory => 
{
    factory.RegisterPacket<Control>();
});

const string Host = "127.0.0.1";
const ushort Port = 57206;

var clientOptions = new TransportOptions 
{ 
    NoDelay = true 
};

using var client = new TcpSession(clientOptions, registry);

Console.WriteLine($"Connecting to {Host}:{Port}...");
try
{
    await client.ConnectAsync(Host, Port).ConfigureAwait(false);
}
catch (Exception ex)
{
    Console.WriteLine($"Connection failed: {ex.Message}");
    Console.WriteLine($"Ensure the Nalix Server is running on port {Port}.");
    return;
}

Console.WriteLine("Connected to server.");

// 2. Warm-up
const int WarmupIterations = 1000;
Console.WriteLine($"\nWarming up ({WarmupIterations} pings)...");
for (int i = 0; i < WarmupIterations; i++)
{
    await client.PingAsync().ConfigureAwait(false);
}

// 3. Benchmark
const int Iterations = 10000;
Console.WriteLine($"Benchmarking RTT ({Iterations} samples)...");

var samples = new double[Iterations];
var sw = Stopwatch.StartNew();

for (int i = 0; i < Iterations; i++)
{
    samples[i] = await client.PingAsync().ConfigureAwait(false);
    
    // Sampling log: Show progress every 500 pings to verify activity without slowing down the test
    if (i > 0 && i % 500 == 0)
    {
        Console.WriteLine($"  -> Step {i}/{Iterations}: Current RTT = {samples[i]:F4} ms");
    }
}

sw.Stop();

// 4. Statistics Calculation
Array.Sort(samples);
double avg = samples.Average();
double median = samples[Iterations / 2];
double p90 = samples[(int)(Iterations * 0.90)];
double p95 = samples[(int)(Iterations * 0.95)];
double p99 = samples[(int)(Iterations * 0.99)];
double p999 = samples[(int)(Iterations * 0.999)];
double min = samples[0];
double max = samples[^1];

// Calculate Standard Deviation (Jitter)
double sumOfSquares = samples.Select(val => (val - avg) * (val - avg)).Sum();
double stdDev = Math.Sqrt(sumOfSquares / Iterations);

// Calculate Throughput
double totalSeconds = sw.Elapsed.TotalSeconds;
double tps = Iterations / totalSeconds;

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
Console.WriteLine($"{"Maximum",-15} | {max,10:F4} | {max * 1000,10:F0}");
Console.WriteLine(new string('-', 40));
Console.WriteLine($"{"Std Deviation",-15} | {stdDev,10:F4} | {stdDev * 1000,10:F0} (Jitter)");
Console.WriteLine($"{"Throughput",-15} | {tps,10:F0} | ops/sec");
Console.WriteLine(new string('=', 40));

Console.WriteLine("\nDone. Press Enter to exit...");

if (!Console.IsInputRedirected)
{
    Console.ReadLine();
}
