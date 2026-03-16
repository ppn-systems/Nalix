using System.Diagnostics;
using System.Net;
using Nalix.Common.Abstractions;
using Nalix.Common.Exceptions;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.SDK.Options;
using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Extensions;

namespace Nalix.Bench;

internal enum BenchmarkMode { Ping, Handshake, Throughput }

internal class Program
{
    private static async Task Main(string[] args)
    {
        var options = CommandLineArgs.Parse(args);
        
        if (options.ShowHelp)
        {
            options.PrintUsage();
            return;
        }

        PrintHeader(options);

        // Security: Handshake requires a pinned Public Key in Nalix 2026+.
        // For testing, we expect the user to provide it or we try to find one.
        if (string.IsNullOrEmpty(options.PublicKey) && (options.Mode == BenchmarkMode.Handshake || options.Mode == BenchmarkMode.Ping))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[WARN] No Server Public Key provided. Handshake might fail if server enforces authentication.");
            Console.ResetColor();
        }

        Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;

        var registry = new PacketRegistry(factory =>
        {
            factory.RegisterPacket<Control>();
            factory.RegisterPacket<Handshake>();
        });

        try
        {
            switch (options.Mode)
            {
                case BenchmarkMode.Ping:
                    await RunPingBenchmark(options, registry).ConfigureAwait(false);
                    break;
                case BenchmarkMode.Handshake:
                    await RunHandshakeBenchmark(options, registry).ConfigureAwait(false);
                    break;
                case BenchmarkMode.Throughput:
                    await RunThroughputBenchmark(options, registry).ConfigureAwait(false);
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[FATAL] Benchmark aborted: {ex.Message}");
            Console.ResetColor();
        }

        Console.WriteLine("\nBenchmark finished. Press any key to exit...");
        if (!Console.IsInputRedirected) Console.ReadKey();
    }

    private static void PrintHeader(CommandLineArgs options)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("=====================================================");
        Console.WriteLine("         NALIX INDUSTRIAL BENCHMARK v1.1.0          ");
        Console.WriteLine("=====================================================");
        Console.ResetColor();
        Console.WriteLine($"Target   : {options.Host}:{options.Port}");
        Console.WriteLine($"Mode     : {options.Mode.ToString().ToUpper()}");
        Console.WriteLine($"Sessions : {options.Sessions}");
        Console.WriteLine($"Capacity : {options.Count} iterations/session");
        
        if (options.Mode == BenchmarkMode.Throughput)
            Console.WriteLine($"Payload  : {options.PayloadSize} bytes");
        
        Console.WriteLine($"Timeout  : {options.TimeoutMs}ms");
        Console.WriteLine("-----------------------------------------------------");
    }

    #region Benchmark Modes

    private static async Task RunPingBenchmark(CommandLineArgs options, IPacketRegistry registry)
    {
        var sessions = await ConnectSessions(options, registry).ConfigureAwait(false);
        if (sessions == null) return;

        try
        {
            if (options.Warmup > 0)
            {
                Console.WriteLine($"[2/4] Warming up ({options.Warmup} iterations)...");
                await Parallel.ForEachAsync(sessions, async (session, ct) =>
                {
                    for (int i = 0; i < options.Warmup; i++)
                    {
                        try { await session.PingAsync(options.TimeoutMs, ct).ConfigureAwait(false); } catch { }
                    }
                }).ConfigureAwait(false);
            }

            Console.WriteLine("[3/4] Preparing controlled environment (GC.Collect)...");
            GC.Collect(2, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();

            Console.WriteLine("[4/4] Executing benchmark...");
            var samples = new double[options.Sessions * options.Count];
            int completed = 0;
            int failed = 0;
            var swBench = Stopwatch.StartNew();

            await Parallel.ForAsync(0, options.Sessions, async (i, ct) =>
            {
                var session = sessions[i];
                for (int j = 0; j < options.Count; j++)
                {
                    try
                    {
                        double rtt = await session.PingAsync(options.TimeoutMs).ConfigureAwait(false);
                        int index = Interlocked.Increment(ref completed) - 1;
                        if (index < samples.Length) samples[index] = rtt;
                    }
                    catch { Interlocked.Increment(ref failed); }

                    if (completed % 10000 == 0 && completed > 0)
                        Console.WriteLine($"      Progress: {completed}/{samples.Length} ({(double)completed / samples.Length:P0})");
                }
            }).ConfigureAwait(false);

            swBench.Stop();
            PrintPingResults(samples, completed, failed, swBench.Elapsed);
        }
        finally
        {
            foreach (var s in sessions) s.Dispose();
        }
    }

    private static async Task RunHandshakeBenchmark(CommandLineArgs options, IPacketRegistry registry)
    {
        Console.WriteLine("[1/2] Executing Handshake Stress Test...");
        int completed = 0;
        int failed = 0;
        var sw = Stopwatch.StartNew();

        var transportOptions = new TransportOptions
        {
            NoDelay = true,
            ConnectTimeoutMillis = options.TimeoutMs,
            ServerPublicKey = options.PublicKey
        };

        await Parallel.ForAsync(0, options.Sessions, async (i, ct) =>
        {
            for (int j = 0; j < options.Count; j++)
            {
                TcpSession? session = null;
                try
                {
                    session = new TcpSession(transportOptions, registry);
                    await session.ConnectAsync(options.Host, options.Port, ct).ConfigureAwait(false);
                    await session.HandshakeAsync(ct).ConfigureAwait(false);
                    Interlocked.Increment(ref completed);
                }
                catch { Interlocked.Increment(ref failed); }
                finally { session?.Dispose(); }

                if (completed % 100 == 0 && completed > 0)
                    Console.WriteLine($"      Handshakes: {completed} (Failed: {failed})");
            }
        }).ConfigureAwait(false);

        sw.Stop();
        PrintHandshakeResults(completed, failed, sw.Elapsed);
    }

    private static async Task RunThroughputBenchmark(CommandLineArgs options, IPacketRegistry registry)
    {
        var sessions = await ConnectSessions(options, registry).ConfigureAwait(false);
        if (sessions == null) return;

        try
        {
            Console.WriteLine("[2/2] Pushing data...");
            int completed = 0;
            int failed = 0;
            var payload = new byte[options.PayloadSize];
            new Random().NextBytes(payload);
            
            var sw = Stopwatch.StartNew();

            await Parallel.ForAsync(0, options.Sessions, async (i, ct) =>
            {
                var session = sessions[i];
                for (int j = 0; j < options.Count; j++)
                {
                    try
                    {
                        await session.SendAsync(payload, encrypt: false, ct).ConfigureAwait(false);
                        Interlocked.Increment(ref completed);
                    }
                    catch { Interlocked.Increment(ref failed); }
                }
            }).ConfigureAwait(false);

            sw.Stop();
            PrintThroughputResults(completed, failed, options.PayloadSize, sw.Elapsed);
        }
        finally
        {
            foreach (var s in sessions) s.Dispose();
        }
    }

    #endregion

    #region Helpers

    private static async Task<TcpSession[]?> ConnectSessions(CommandLineArgs options, IPacketRegistry registry)
    {
        Console.WriteLine($"[1/4] Connecting {options.Sessions} sessions...");
        var transportOptions = new TransportOptions 
        { 
            NoDelay = true, 
            ConnectTimeoutMillis = options.TimeoutMs,
            ServerPublicKey = options.PublicKey
        };
        
        var sessions = new TcpSession[options.Sessions];
        var swTotal = Stopwatch.StartNew();

        try
        {
            var connectTasks = new Task[options.Sessions];
            for (int i = 0; i < options.Sessions; i++)
            {
                var session = new TcpSession(transportOptions, registry);
                sessions[i] = session;
                connectTasks[i] = session.ConnectAsync(options.Host, options.Port);

                if (i % 20 == 0 && i > 0) await Task.Delay(5).ConfigureAwait(false);
            }

            await Task.WhenAll(connectTasks).ConfigureAwait(false);
            Console.WriteLine($"[OK] All sessions connected in {swTotal.ElapsedMilliseconds}ms.");
            return sessions;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[FAIL] Connection failed: {ex.Message}");
            Console.ResetColor();
            foreach (var s in sessions) s?.Dispose();
            return null;
        }
    }

    private static void PrintPingResults(double[] samples, int completed, int failed, TimeSpan duration)
    {
        var validSamples = samples.Where(s => s > 0).OrderBy(s => s).ToList();
        int count = validSamples.Count;

        if (count == 0) { Console.WriteLine("\n[ERROR] No valid samples collected."); return; }

        double avg = validSamples.Average();
        double max = validSamples[^1], median = validSamples[count / 2];
        double p99 = validSamples[(int)(count * 0.99)], p999 = validSamples[(int)(count * 0.999)];
        double stdDev = Math.Sqrt(validSamples.Sum(d => Math.Pow(d - avg, 2)) / count);
        double tps = count / duration.TotalSeconds;

        Console.WriteLine("\n" + new string('=', 60));
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("                   PING LATENCY RESULTS                   ");
        Console.ResetColor();
        Console.WriteLine(new string('=', 60));
        Console.WriteLine($"{"Metric",-20} | {"Value",-15} | {"Description",-20}");
        Console.WriteLine(new string('-', 60));
        Console.WriteLine($"{"Throughput",-20} | {tps,10:F0} ops/s | pings resolved per second");
        Console.WriteLine($"{"Success Rate",-20} | {(double)completed / (completed + failed),10:P2} | Success/Fail ratio");
        Console.WriteLine($"{"Average RTT",-20} | {avg,10:F4} ms    | Mean latency");
        Console.WriteLine($"{"Standard Dev.",-20} | {stdDev,10:F4} ms    | Jitter (stability)");
        Console.WriteLine(new string('-', 60));
        Console.WriteLine($"{"P50 (Median)",-20} | {median,10:F4} ms    |");
        Console.WriteLine($"{"P99",-20} | {p99,10:F4} ms    |");
        Console.WriteLine($"{"P99.9",-20} | {p999,10:F4} ms    |");
        Console.WriteLine($"{"Maximum",-20} | {max,10:F4} ms    |");
        Console.WriteLine(new string('=', 60));
    }

    private static void PrintHandshakeResults(int completed, int failed, TimeSpan duration)
    {
        double hps = completed / duration.TotalSeconds;
        Console.WriteLine("\n" + new string('=', 60));
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("                   HANDSHAKE STRESS RESULTS               ");
        Console.ResetColor();
        Console.WriteLine(new string('=', 60));
        Console.WriteLine($"{"Metric",-20} | {"Value",-15}");
        Console.WriteLine(new string('-', 60));
        Console.WriteLine($"{"Total Successful",-20} | {completed,10}");
        Console.WriteLine($"{"Total Failed",-20} | {failed,10}");
        Console.WriteLine($"{"Handshakes/sec",-20} | {hps,10:F2}");
        Console.WriteLine($"{"Avg Time/HS",-20} | {1000.0 / hps,10:F2} ms");
        Console.WriteLine(new string('=', 60));
    }

    private static void PrintThroughputResults(int completed, int failed, int payloadSize, TimeSpan duration)
    {
        double pps = completed / duration.TotalSeconds;
        double mbps = (pps * payloadSize) / (1024.0 * 1024.0);
        Console.WriteLine("\n" + new string('=', 60));
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("                   THROUGHPUT RESULTS                     ");
        Console.ResetColor();
        Console.WriteLine(new string('=', 60));
        Console.WriteLine($"{"Metric",-20} | {"Value",-15}");
        Console.WriteLine(new string('-', 60));
        Console.WriteLine($"{"Packets Sent",-20} | {completed,10}");
        Console.WriteLine($"{"Payload Size",-20} | {payloadSize,10} bytes");
        Console.WriteLine($"{"Total Failed",-20} | {failed,10}");
        Console.WriteLine($"{"Packets/sec",-20} | {pps,10:F0}");
        Console.WriteLine($"{"Throughput",-20} | {mbps,10:F2} MB/s");
        Console.WriteLine(new string('=', 60));
    }

    #endregion
}

internal class CommandLineArgs
{
    public string Host { get; set; } = "127.0.0.1";
    public ushort Port { get; set; } = 57206;
    public int Sessions { get; set; } = 100;
    public int Count { get; set; } = 1000;
    public int TimeoutMs { get; set; } = 5000;
    public int Warmup { get; set; } = 200;
    public int PayloadSize { get; set; } = 1024;
    public string? PublicKey { get; set; }
    public BenchmarkMode Mode { get; set; } = BenchmarkMode.Ping;
    public bool ShowHelp { get; set; }

    public static CommandLineArgs Parse(string[] args)
    {
        var options = new CommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--help":
                case "-h": options.ShowHelp = true; break;
                case "--host": options.Host = args[++i]; break;
                case "--port":
                case "-p": options.Port = ushort.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
                case "--sessions":
                case "-s": options.Sessions = int.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
                case "--count":
                case "-n": options.Count = int.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
                case "--timeout":
                case "-t": options.TimeoutMs = int.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
                case "--warmup": options.Warmup = int.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
                case "--payload":
                case "-b": options.PayloadSize = int.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
                case "--key":
                case "-k": options.PublicKey = args[++i]; break;
                case "--mode":
                case "-m":
                    options.Mode = args[++i].ToLower() switch
                    {
                        "ping" => BenchmarkMode.Ping,
                        "handshake" => BenchmarkMode.Handshake,
                        "throughput" => BenchmarkMode.Throughput,
                        _ => throw new ArgumentException($"Unknown mode: {args[i]}")
                    };
                    break;
            }
        }
        return options;
    }

    public void PrintUsage()
    {
        Console.WriteLine("Usage: Nalix.Bench [options]");
        Console.WriteLine("Options:");
        Console.WriteLine("  -m, --mode <mode>       Benchmark mode: ping, handshake, throughput (default: ping)");
        Console.WriteLine("  -h, --host <host>       Target host (default: 127.0.0.1)");
        Console.WriteLine("  -p, --port <port>       Target port (default: 57206)");
        Console.WriteLine("  -s, --sessions <num>    Number of concurrent sessions (default: 100)");
        Console.WriteLine("  -n, --count <num>       Iterations per session (default: 1000)");
        Console.WriteLine("  -t, --timeout <ms>      Timeout per operation in ms (default: 5000)");
        Console.WriteLine("  -b, --payload <bytes>   Payload size for throughput test (default: 1024)");
        Console.WriteLine("  -k, --key <hex>         Server Public Key for handshake authentication");
        Console.WriteLine("  --warmup <num>          Number of warmup iterations (default: 200)");
    }
}
