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

    public ConnectionWorker(LoadTestOptions options, ILoadScenario scenario, MetricsCollector metrics)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using TcpSession session = new(new TransportOptions
                {
                    Address = _options.Host,
                    Port = _options.Port
                });

                await session.ConnectAsync(ct: cancellationToken).ConfigureAwait(false);

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
        if (kind == ErrorKind.Other && count <= 5)
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
