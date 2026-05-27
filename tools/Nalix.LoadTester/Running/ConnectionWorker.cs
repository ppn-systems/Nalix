// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Net.Sockets;
using Nalix.LoadTester.Metrics;
using Nalix.LoadTester.Scenarios;
using Nalix.SDK.Options;
using Nalix.SDK.Transport;

namespace Nalix.LoadTester.Running;

internal sealed class ConnectionWorker
{
    private readonly LoadTestOptions _options;
    private readonly ILoadScenario _scenario;
    private readonly MetricsCollector _metrics;
    private readonly WorkloadState _state;

    public ConnectionWorker(
        LoadTestOptions options,
        ILoadScenario scenario,
        MetricsCollector metrics,
        WorkloadState state)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _state = state ?? throw new ArgumentNullException(nameof(state));
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _state.WorkerStarted();
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    TransportOptions transportOptions = new()
                    {
                        Address = _options.Host,
                        Port = _options.Port
                    };

                    byte[]? proxyHeader = null;
                    if (_options.UseProxyProtocol)
                    {
                        proxyHeader = new byte[28];
                        "\r\n\r\n\0\r\nQUIT\n"u8.CopyTo(proxyHeader);
                        proxyHeader[12] = 0x21; // Version 2, Command PROXY
                        proxyHeader[13] = 0x11; // AF_INET, STREAM
                        System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(proxyHeader.AsSpan(14, 2), 12); // Length

                        // Src IP
                        proxyHeader[16] = (byte)Random.Shared.Next(1, 255);
                        proxyHeader[17] = (byte)Random.Shared.Next(0, 256);
                        proxyHeader[18] = (byte)Random.Shared.Next(0, 256);
                        proxyHeader[19] = (byte)Random.Shared.Next(1, 255);

                        // Dst IP
                        if (System.Net.IPAddress.TryParse(_options.Host, out System.Net.IPAddress? hostIp) && hostIp.AddressFamily == AddressFamily.InterNetwork)
                        {
                            hostIp.TryWriteBytes(proxyHeader.AsSpan(20, 4), out _);
                        }

                        // Ports
                        System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(proxyHeader.AsSpan(24, 2), (ushort)Random.Shared.Next(1024, 65535));
                        System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(proxyHeader.AsSpan(26, 2), _options.Port);
                    }

                    using TcpSession session = new(transportOptions);

                    if (proxyHeader != null)
                    {
                        await session.ConnectWithProxyAsync(proxyHeader, ct: cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        await session.ConnectAsync(ct: cancellationToken).ConfigureAwait(false);
                    }

                    // Send exactly ONE warmup packet to warm up JIT & pools on server/client side
                    try
                    {
                        using CancellationTokenSource warmupCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        warmupCts.CancelAfter(TimeSpan.FromSeconds(15)); // Extended timeout for JIT overhead
                        await _scenario.ExecuteAsync(session, warmupCts.Token).ConfigureAwait(false);
                    }
                    catch (Exception) when (!cancellationToken.IsCancellationRequested)
                    {
                        // Non-fatal warning if warmup fails but connection is still active
                    }

                    while (!cancellationToken.IsCancellationRequested && session.IsConnected)
                    {
                        await ExecuteRequestAsync(session, cancellationToken).ConfigureAwait(false);
                        await Task.Yield();
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    RecordFailure(ex);
                    await DelayBeforeReconnectAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            _state.WorkerStopped();
        }
    }

    private async Task ExecuteRequestAsync(TcpSession session, CancellationToken cancellationToken)
    {
        try
        {
            Double latencyMs = await _scenario.ExecuteAsync(session, cancellationToken).ConfigureAwait(false);
            _metrics.RecordSuccess(latencyMs);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException ex)
        {
            RecordFailure(ex);
        }
        catch (SocketException ex)
        {
            RecordFailure(ex);
        }
        catch (IOException ex)
        {
            RecordFailure(ex);
        }
        catch (Exception ex)
        {
            RecordFailure(ex);
        }
    }

    private void RecordFailure(Exception exception)
    {
        ErrorKind kind = exception switch
        {
            TimeoutException => ErrorKind.Timeout,
            SocketException => ErrorKind.Socket,
            IOException => ErrorKind.Socket,
            _ => ErrorKind.Other
        };

        Int64 count = _metrics.RecordFailure(kind);
        if (kind == ErrorKind.Other && count is > 0 and <= 5)
        {
            Console.Error.WriteLine($"[ERROR] Unexpected client exception: {exception.GetType().Name}: {exception.Message}");
        }
    }

    private static async Task DelayBeforeReconnectAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(500, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }
}
