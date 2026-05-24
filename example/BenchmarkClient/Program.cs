// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using Nalix.Codec.DataFrames;
using Nalix.Codec.ProtocolFrames;
using Nalix.SDK.Options;
using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Extensions;

namespace BenchmarkClient;

public static class Program
{
    private static long s_successfulPings;
    private static long s_failedPings;
    private static long s_timeoutErrors;
    private static long s_socketErrors;
    private static long s_otherErrors;
    private static long s_totalRttTicks;

    private static int s_pingSequence;

    // RTT samples ring buffer (bound RAM usage to ~80MB)
    private static double[] s_rttSamples = Array.Empty<double>();
    private static long s_sampleIndex;

    public static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("=========================================================");
        Console.WriteLine("         Nalix High-Performance Load Testing Client       ");
        Console.WriteLine("=========================================================");

        // Parse target parameters
        string host = "127.0.0.1";
        ushort port = 57206; // Default Backend example port
        int concurrentConnections = 50;
        int durationSeconds = 15;
        int timeoutMs = 5000;
        int payloadSize = 1500; // Default to 1.5 KB payload for the example project

        if (args.Length > 0)
        {
            host = args[0];
        }

        if (args.Length > 1 && ushort.TryParse(args[1], out ushort p))
        {
            port = p;
        }

        if (args.Length > 2 && int.TryParse(args[2], out int c))
        {
            concurrentConnections = c;
        }

        if (args.Length > 3 && int.TryParse(args[3], out int d))
        {
            durationSeconds = d;
        }

        if (args.Length > 4 && int.TryParse(args[4], out int t))
        {
            timeoutMs = t;
        }

        if (args.Length > 5 && int.TryParse(args[5], out int s))
        {
            payloadSize = s;
        }

        Console.WriteLine($"Target Host        : {host}");
        Console.WriteLine($"Target Port        : {port}");
        Console.WriteLine($"Concurrent Clients : {concurrentConnections}");
        Console.WriteLine($"Duration           : {durationSeconds} seconds");
        Console.WriteLine($"Ping Timeout       : {timeoutMs} ms");
        Console.WriteLine($"Payload Size       : {payloadSize} bytes");
        Console.WriteLine("---------------------------------------------------------");

        // Initialize RTT sample ring buffer
        s_rttSamples = new double[10_000_000];

        // Build packet metadata catalog (required by Nalix SDK & Codec)
        if (!PacketRegistry.IsBuilt)
        {
            PacketRegistry.Build();
        }

        Console.WriteLine($"[DEBUG] Client BenchmarkPacket Magic: 0x{Nalix.Codec.DataFrames.PacketSchema<Nalix.Codec.ProtocolFrames.BenchmarkPacket>.AutoMagic:X8}");

        using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(durationSeconds));
        CancellationToken token = cts.Token;

        // Start connection workers
        Task[] tasks = new Task[concurrentConnections];
        Stopwatch stopwatch = Stopwatch.StartNew();

        Console.WriteLine("Launching simulated benchmark connections...");
        for (int i = 0; i < concurrentConnections; i++)
        {
            tasks[i] = RunClientWorkerAsync(host, port, timeoutMs, payloadSize, token);
        }

        // Periodically report status
        _ = ReportProgressAsync(stopwatch, token);

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Normal termination when time limit is reached
        }

        stopwatch.Stop();
        Console.WriteLine("\n---------------------------------------------------------");
        Console.WriteLine("Benchmark Completed!");

        long totalPings = Volatile.Read(ref s_successfulPings);
        long totalFailed = Volatile.Read(ref s_failedPings);
        double duration = stopwatch.Elapsed.TotalSeconds;
        double rps = totalPings / duration;

        Console.WriteLine($"Elapsed Time       : {duration:F2} seconds");
        Console.WriteLine($"Successful Pings   : {totalPings}");
        Console.WriteLine($"Failed Pings       : {totalFailed}");
        if (totalFailed > 0)
        {
            Console.WriteLine($"  -> Timeouts      : {Volatile.Read(ref s_timeoutErrors)}");
            Console.WriteLine($"  -> Socket Drops  : {Volatile.Read(ref s_socketErrors)}");
            Console.WriteLine($"  -> Other Errors  : {Volatile.Read(ref s_otherErrors)}");
        }
        Console.WriteLine($"RPS (Throughput)   : {rps:F2} pings/sec");

        long sampleCount = Math.Min(Volatile.Read(ref s_sampleIndex), s_rttSamples.Length);
        if (sampleCount > 0)
        {
            // Copy active portion of samples to a temp array and sort for accurate percentile calculation
            double[] activeSamples = new double[sampleCount];
            Array.Copy(s_rttSamples, 0, activeSamples, 0, sampleCount);
            Array.Sort(activeSamples);

            double p50 = activeSamples[(int)(sampleCount * 0.50)];
            double p95 = activeSamples[(int)(sampleCount * 0.95)];
            double p99 = activeSamples[(int)(sampleCount * 0.99)];
            double p999 = activeSamples[(int)(sampleCount * 0.999)];

            Console.WriteLine($"Average Latency    : {(double)Volatile.Read(ref s_totalRttTicks) / totalPings:F2} ms");
            Console.WriteLine($"P50 (Median)       : {p50:F2} ms");
            Console.WriteLine($"P95 Latency        : {p95:F2} ms");
            Console.WriteLine($"P99 Latency        : {p99:F2} ms");
            Console.WriteLine($"P99.9 Latency      : {p999:F2} ms");
        }
        Console.WriteLine("=========================================================");
    }

    private static async Task RunClientWorkerAsync(string host, ushort port, int timeoutMs, int payloadSize, CancellationToken ct)
    {
        byte[]? payload = payloadSize > 0 ? new byte[payloadSize] : null;
        if (payload != null)
        {
            // Fill with dummy data so it's not all zeros
            new Random().NextBytes(payload);
        }

        while (!ct.IsCancellationRequested)
        {
            try
            {
                using TcpSession session = new TcpSession(new TransportOptions
                {
                    Address = host,
                    Port = port
                });

                await session.ConnectAsync(ct: ct).ConfigureAwait(false);

                while (!ct.IsCancellationRequested && session.IsConnected)
                {
                    Stopwatch sw = Stopwatch.StartNew();
                    try
                    {
                        double rtt;
                        if (payloadSize > 0)
                        {
                            ushort seq = unchecked((ushort)Interlocked.Increment(ref s_pingSequence));
                            BenchmarkPacket packet = BenchmarkPacket.Create();
                            packet.SequenceId = seq;
                            packet.Payload = payload;

                            try
                            {
                                using BenchmarkPacket response = await session.RequestAsync<BenchmarkPacket>(
                                    packet,
                                    options: RequestOptions.Default.WithTimeout(timeoutMs),
                                    predicate: p => p.SequenceId == seq,
                                    ct: ct).ConfigureAwait(false);

                                sw.Stop();
                                rtt = sw.Elapsed.TotalMilliseconds;
                            }
                            finally
                            {
                                packet.Dispose();
                            }
                        }
                        else
                        {
                            // Perform end-to-end ping over Nalix.Codec
                            rtt = await session.PingAsync(timeoutMs: timeoutMs, ct: ct).ConfigureAwait(false);
                            sw.Stop();
                        }

                        _ = Interlocked.Increment(ref s_successfulPings);

                        // Lock-free storage of sample in ring buffer
                        long idx = Interlocked.Increment(ref s_sampleIndex) - 1;
                        s_rttSamples[idx % s_rttSamples.Length] = rtt;

                        long rttMs = (long)Math.Round(rtt);
                        _ = Interlocked.Add(ref s_totalRttTicks, rttMs);
                    }
                    catch (TimeoutException)
                    {
                        _ = Interlocked.Increment(ref s_failedPings);
                        _ = Interlocked.Increment(ref s_timeoutErrors);
                    }
                    catch (System.Net.Sockets.SocketException)
                    {
                        _ = Interlocked.Increment(ref s_failedPings);
                        _ = Interlocked.Increment(ref s_socketErrors);
                    }
                    catch (Exception ex)
                    {
                        _ = Interlocked.Increment(ref s_failedPings);
                        if (ex is System.IO.IOException)
                        {
                            _ = Interlocked.Increment(ref s_socketErrors);
                        }
                        else
                        {
                            if (Interlocked.Increment(ref s_otherErrors) <= 5)
                            {
                                Console.Error.WriteLine($"[ERROR] Unexpected client exception: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                            }
                        }
                    }

                    // Yield control to allow cooperative multitasking, avoiding CPU starvation
                    await Task.Yield();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _ = Interlocked.Increment(ref s_failedPings);
                if (ex is System.Net.Sockets.SocketException || ex is System.IO.IOException)
                {
                    _ = Interlocked.Increment(ref s_socketErrors);
                }
                else
                {
                    _ = Interlocked.Increment(ref s_otherErrors);
                }

                // Back-off before reconnecting
                await Task.Delay(500, ct).ConfigureAwait(false);
            }
        }
    }

    private static async Task ReportProgressAsync(Stopwatch sw, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(1000, ct).ConfigureAwait(false);
                long currentSuccess = Volatile.Read(ref s_successfulPings);
                long currentFailed = Volatile.Read(ref s_failedPings);
                double elapsed = sw.Elapsed.TotalSeconds;

                Console.WriteLine($"[{elapsed:F0}s] Successful: {currentSuccess} | Failed: {currentFailed} | Current RPS: {currentSuccess / elapsed:F1}");
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
