// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Codec.DataFrames;
using Nalix.Codec.ProtocolFrames;
using Nalix.SDK.Options;
using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Extensions;

namespace UdpLoadTester;

public static class Program
{
    private static long s_totalSent;
    private static long s_totalRecv;
    private static long s_totalBytes;
    private static long s_totalErrors;
    private static int s_activeClients;

    public static async Task<int> Main(string[] args)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("********************************************************************************");
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("                     NALIX UDP LOAD TESTING UTILITY                             ");
        Console.WriteLine("                     FOR AUTHORIZED PRIVATE TESTING ONLY                        ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("********************************************************************************");
        Console.ResetColor();
        Console.WriteLine("WARNING: This tool is designed to verify local/LAN UDP hot-path fixes.");
        Console.WriteLine("DO NOT run this tool against public endpoints or networks without authorization.");
        Console.WriteLine("--------------------------------------------------------------------------------");

        // Parse CLI arguments
        string host = GetArgValue(args, "--host", "127.0.0.1");
        int port = int.Parse(GetArgValue(args, "--port", "57206"));
        int durationSec = int.Parse(GetArgValue(args, "--duration", "30"));
        int clientCount = int.Parse(GetArgValue(args, "--clients", "10"));
        int pps = int.Parse(GetArgValue(args, "--pps", "10")); // packets per second per client
        int payloadSize = int.Parse(GetArgValue(args, "--payload-size", "23"));
        bool encrypt = bool.Parse(GetArgValue(args, "--encrypt", "false"));
        bool force = args.Contains("--force");

        if (args.Contains("--help") || args.Contains("-h"))
        {
            Console.WriteLine("Options:");
            Console.WriteLine("  --host <string>          Target host address (default: 127.0.0.1)");
            Console.WriteLine("  --port <int>             Target server port (default: 57206)");
            Console.WriteLine("  --duration <int>         Testing duration in seconds (default: 30)");
            Console.WriteLine("  --clients <int>          Number of concurrent clients (default: 10)");
            Console.WriteLine("  --pps <int>              Packets per second per client (default: 10)");
            Console.WriteLine("  --payload-size <int>     UDP payload size in bytes (default: 23)");
            Console.WriteLine("  --encrypt <bool>         Encrypt UDP payload (default: false)");
            Console.WriteLine("  --force                  Bypass WAN/Public IP guard");
            return 0;
        }

        // WAN/Public IP Safety Guard
        if (!force && !IsLocalOrLan(host))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"CRITICAL ERROR: Destination '{host}' resolves to a WAN/Public address.");
            Console.WriteLine("Load testing public hosts is restricted by safety policy.");
            Console.WriteLine("To bypass this (authorized tests only), append the '--force' flag.");
            Console.ResetColor();
            return 1;
        }

        // Validate payload size matches TimeSync length (23 bytes) when unencrypted
        if (!encrypt && payloadSize != 23)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"WARNING: The backend enforces strict deserialization checks. TimeSync packet size is exactly 23 bytes.");
            Console.WriteLine($"Adding trailing padding bytes (configured payload size {payloadSize}) will trigger deserialization failures.");
            Console.WriteLine($"Reverting payload size to 23 bytes.");
            Console.ResetColor();
            payloadSize = 23;
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"Target:      {host}:{port}");
        Console.WriteLine($"Clients:     {clientCount}");
        Console.WriteLine($"PPS/Client:  {pps} (Total Target PPS: {clientCount * pps})");
        Console.WriteLine($"Payload:     {payloadSize} bytes");
        Console.WriteLine($"Duration:    {durationSec} seconds");
        Console.WriteLine($"Encryption:  {(encrypt ? "Enabled" : "Disabled")}");
        Console.WriteLine("--------------------------------------------------------------------------------");
        Console.ResetColor();

        // Build the process-wide PacketRegistry
        if (!PacketRegistry.IsBuilt)
        {
            PacketRegistry.Build();
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(durationSec + 5)); // Hard timeout buffer
        var testTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
        testTimeoutCts.CancelAfter(TimeSpan.FromSeconds(durationSec));

        // Start background statistics reporter
        var statsTask = Task.Run(() => ReportStatsAsync(pps * clientCount, testTimeoutCts.Token));

        var clients = new Task[clientCount];
        for (int i = 0; i < clientCount; i++)
        {
            int clientId = i + 1;
            clients[i] = Task.Run(() => RunClientAsync(clientId, host, (ushort)port, pps, payloadSize, encrypt, testTimeoutCts.Token));
        }

        try
        {
            await Task.WhenAll(clients).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[Error] Tester loop interrupted: {ex.Message}");
            Console.ResetColor();
        }

        // Wait for stats task to complete
        testTimeoutCts.Cancel();
        try
        {
            await statsTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }

        Console.WriteLine("--------------------------------------------------------------------------------");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("TEST COMPLETED");
        Console.ResetColor();
        Console.WriteLine($"Total Sent:     {Volatile.Read(ref s_totalSent)}");
        Console.WriteLine($"Total Received: {Volatile.Read(ref s_totalRecv)}");
        Console.WriteLine($"Total Errors:   {Volatile.Read(ref s_totalErrors)}");
        Console.WriteLine($"Loss Rate:      {CalculateLossRate():F2}%");

        return 0;
    }

    private static async Task RunClientAsync(int clientId, string host, ushort port, int pps, int payloadSize, bool encrypt, CancellationToken ct)
    {
        TransportOptions options = new()
        {
            Address = host,
            Port = port,
            ReconnectEnabled = false,
            KeepAliveIntervalMillis = 5000
        };

        TcpSession? tcpSession = null;
        UdpSession? udpSession = null;
        bool activeClientRegistered = false;

        try
        {
            // 1. Establish TCP login session to get Snowflake SessionToken
            tcpSession = new TcpSession(options);
            await tcpSession.ConnectAsync(ct: ct).ConfigureAwait(false);
            
            // X25519 Handshake required to authenticate and get SessionToken from server
            await tcpSession.HandshakeAsync(ct).ConfigureAwait(false);

            ulong token = tcpSession.State.SessionToken;
            if (token == 0)
            {
                throw new InvalidOperationException("Handshake succeeded but session token is 0.");
            }

            // 2. Instantiate companion UdpSession sharing the established TcpSession state
            udpSession = new UdpSession(options, tcpSession.State);
            await udpSession.ConnectAsync(ct: ct).ConfigureAwait(false);

            Interlocked.Increment(ref s_activeClients);
            activeClientRegistered = true;

            // Register response listener
            udpSession.OnMessageReceived += (sender, lease) =>
            {
                try
                {
                    IPacket packet = PacketRegistry.Deserialize(lease.Span);
                    if (packet is TimeSync ts && ts.Type == ControlType.TIMESYNCRESPONSE)
                    {
                        Interlocked.Increment(ref s_totalRecv);
                    }
                }
                catch
                {
                    Interlocked.Increment(ref s_totalErrors);
                }
            };

            ushort seq = 0;
            double intervalMs = 1000.0 / pps;
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(intervalMs));

            // Pre-allocate payload padding buffer
            var timeSync = new TimeSync();
            int headerAndMinPayloadSize = timeSync.Length;
            int finalPayloadSize = Math.Max(payloadSize, headerAndMinPayloadSize);
            byte[] sendBuffer = new byte[finalPayloadSize];

            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                seq++;
                
                // Re-initialize TimeSync parameters
                timeSync.Initialize(
                    ControlType.TIMESYNCREQUEST,
                    seq,
                    flags: PacketFlags.SYSTEM | PacketFlags.UNRELIABLE
                );

                // Serialize into raw buffer
                int written = timeSync.Serialize(sendBuffer);
                
                // Zero-out the remaining padding section if payload size > packet size
                if (finalPayloadSize > written)
                {
                    Array.Clear(sendBuffer, written, finalPayloadSize - written);
                }

                try
                {
                    // Send over UDP (reusing UdpSession socket)
                    await udpSession.SendAsync(sendBuffer.AsMemory(), encrypt: encrypt, ct: ct).ConfigureAwait(false);
                    Interlocked.Increment(ref s_totalSent);
                    Interlocked.Add(ref s_totalBytes, finalPayloadSize);
                }
                catch
                {
                    Interlocked.Increment(ref s_totalErrors);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref s_totalErrors);
            Console.WriteLine($"[Client {clientId} Error]: {ex.Message}");
        }
        finally
        {
            if (activeClientRegistered)
            {
                Interlocked.Decrement(ref s_activeClients);
            }

            if (udpSession != null)
            {
                udpSession.Dispose();
            }
            if (tcpSession != null)
            {
                await tcpSession.DisconnectAsync().ConfigureAwait(false);
                tcpSession.Dispose();
            }
        }
    }

    private static async Task ReportStatsAsync(int targetPps, CancellationToken ct)
    {
        long lastSent = 0;
        long lastBytes = 0;
        var stopwatch = Stopwatch.StartNew();

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(1000, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            long currentSent = Volatile.Read(ref s_totalSent);
            long currentBytes = Volatile.Read(ref s_totalBytes);

            double intervalSec = stopwatch.Elapsed.TotalSeconds;
            stopwatch.Restart();

            double ppsReal = (currentSent - lastSent) / intervalSec;
            double bpsReal = (currentBytes - lastBytes) / intervalSec;

            lastSent = currentSent;
            lastBytes = currentBytes;

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Active Clients: {Volatile.Read(ref s_activeClients)} | Sent: {currentSent} ({ppsReal:F0} / {targetPps} pkts/s, {bpsReal / 1024.0:F1} KB/s) | Recv: {Volatile.Read(ref s_totalRecv)} | Errors: {Volatile.Read(ref s_totalErrors)}");
        }
    }

    private static double CalculateLossRate()
    {
        long sent = Volatile.Read(ref s_totalSent);
        long recv = Volatile.Read(ref s_totalRecv);
        if (sent == 0) return 0.0;
        long lost = sent - recv;
        return lost > 0 ? (lost * 100.0) / sent : 0.0;
    }

    private static string GetArgValue(string[] args, string flag, string defaultValue)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }
        return defaultValue;
    }

    private static bool IsLocalOrLan(string host)
    {
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        try
        {
            if (IPAddress.TryParse(host, out var ip))
            {
                return IsLocalOrLanIp(ip);
            }

            var addresses = Dns.GetHostAddresses(host);
            return addresses.Length > 0 && addresses.All(IsLocalOrLanIp);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsLocalOrLanIp(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip))
        {
            return true;
        }

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            byte[] bytes = ip.GetAddressBytes();
            
            // 10.0.0.0/8
            if (bytes[0] == 10)
            {
                return true;
            }
            
            // 172.16.0.0/12
            if (bytes[0] == 172 && (bytes[1] >= 16 && bytes[1] <= 31))
            {
                return true;
            }
            
            // 192.168.0.0/16
            if (bytes[0] == 192 && bytes[1] == 168)
            {
                return true;
            }
            
            // 169.254.0.0/16
            if (bytes[0] == 169 && bytes[1] == 254)
            {
                return true;
            }
        }
        else if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (ip.Equals(IPAddress.IPv6Loopback))
            {
                return true;
            }
            
            byte[] bytes = ip.GetAddressBytes();
            // fc00::/7 (Unique Local) or fe80::/10 (Link-Local)
            if ((bytes[0] & 0xFE) == 0xFC || (bytes[0] == 0xFE && (bytes[1] & 0xC0) == 0x80))
            {
                return true;
            }
        }

        return false;
    }
}
