// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Codec.DataFrames;
using Nalix.SDK.Options;
using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Extensions;

namespace Nalix.BenchmarkClient;

public static class Program
{
    private static long s_successfulPings;
    private static long s_failedPings;
    private static long s_totalRttTicks;

    public static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("=========================================================");
        Console.WriteLine("         Nalix High-Performance Load Testing Client       ");
        Console.WriteLine("=========================================================");

        // Parse target parameters
        string host = "127.0.0.1";
        ushort port = 5200; // Default Backend example port
        int concurrentConnections = 50;
        int durationSeconds = 15;

        if (args.Length > 0) host = args[0];
        if (args.Length > 1 && ushort.TryParse(args[1], out var p)) port = p;
        if (args.Length > 2 && int.TryParse(args[2], out var c)) concurrentConnections = c;
        if (args.Length > 3 && int.TryParse(args[3], out var d)) durationSeconds = d;

        Console.WriteLine($"Target Host        : {host}");
        Console.WriteLine($"Target Port        : {port}");
        Console.WriteLine($"Concurrent Clients : {concurrentConnections}");
        Console.WriteLine($"Duration           : {durationSeconds} seconds");
        Console.WriteLine("---------------------------------------------------------");

        // Build packet metadata catalog (required by Nalix SDK & Codec)
        if (!PacketRegistry.IsBuilt)
        {
            PacketRegistry.Build();
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(durationSeconds));
        var token = cts.Token;

        // Start connection workers
        var tasks = new Task[concurrentConnections];
        var stopwatch = Stopwatch.StartNew();

        Console.WriteLine("Launching simulated benchmark connections...");
        for (int i = 0; i < concurrentConnections; i++)
        {
            tasks[i] = RunClientWorkerAsync(host, port, token);
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
        Console.WriteLine($"RPS (Throughput)   : {rps:F2} pings/sec");
        
        if (totalPings > 0)
        {
            double avgRtt = (double)Volatile.Read(ref s_totalRttTicks) / totalPings;
            Console.WriteLine($"Average Latency    : {avgRtt:F2} ms");
        }
        Console.WriteLine("=========================================================");
    }

    private static async Task RunClientWorkerAsync(string host, ushort port, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var session = new TcpSession(new TransportOptions
                {
                    Address = host,
                    Port = port
                });

                await session.ConnectAsync(ct: ct).ConfigureAwait(false);

                while (!ct.IsCancellationRequested && session.IsConnected)
                {
                    var sw = Stopwatch.StartNew();
                    try
                    {
                        // Use SDK PingAsync extension to perform real, end-to-end ping over Nalix.Codec
                        double rtt = await session.PingAsync(timeoutMs: 1000, ct: ct).ConfigureAwait(false);
                        sw.Stop();

                        Interlocked.Increment(ref s_successfulPings);
                        
                        // Approximate RTT to long ticks/ms safely
                        long rttMs = (long)Math.Round(rtt);
                        Interlocked.Add(ref s_totalRttTicks, rttMs);
                    }
                    catch (Exception)
                    {
                        Interlocked.Increment(ref s_failedPings);
                    }

                    // Max speed: yield control to allow cooperative multitasking, but no sleep delay
                    await Task.Yield();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception)
            {
                Interlocked.Increment(ref s_failedPings);
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

                Console.WriteLine($"[{elapsed:F0}s] Successful: {currentSuccess} | Failed: {currentFailed} | Current RPS: {(currentSuccess / elapsed):F1}");
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
