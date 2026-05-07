// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using SDK.Core;
using SDK.UI;

namespace SDK.Services;

/// <summary>
/// Background keepalive that periodically pings the server.
/// Single responsibility: manage the keepalive lifecycle.
/// </summary>
internal sealed class KeepAliveService : IDisposable
{
    private readonly ClientSession _client;
    private readonly StatusBar _status;
    private readonly EventLog _log;
    private readonly PingChart _chart;

#pragma warning disable CA2213 // Disposable fields should be disposed
    private CancellationTokenSource? _cts;
    private Int32 _pauseCount;
#pragma warning restore CA2213 // Disposable fields should be disposed

    private readonly SemaphoreSlim _pingGate = new(1, 1);

    public KeepAliveService(ClientSession client, StatusBar status, EventLog log, PingChart chart)
    {
        _client = client;
        _status = status;
        _log = log;
        _chart = chart;
    }

    /// <summary>Starts background keepalive (idempotent — stops previous if running).</summary>
    public void Start()
    {
        this.Stop();
        _cts = new CancellationTokenSource();
        CancellationToken ct = _cts.Token;

        _ = Task.Run(async () =>
        {
            const int IntervalMs = 15_000;
            _log.Info("Background keepalive started (15s interval, using PingAsync).");

            while (!ct.IsCancellationRequested && _client.IsConnected)
            {
#pragma warning disable CA1031 // Do not catch general exception types
                try
                {
                    await Task.Delay(IntervalMs, ct).ConfigureAwait(false);
                    if (ct.IsCancellationRequested || !_client.IsConnected)
                    {
                        break;
                    }

                    if (Volatile.Read(ref _pauseCount) > 0)
                    {
                        continue;
                    }

                    using IDisposable __ = await this.EnterPingScopeAsync(ct).ConfigureAwait(false);
                    double ms = await _client.PingOnceAsync(timeoutMs: 5000, ct).ConfigureAwait(false);
                    _status.UpdatePing(ms);
                    _chart.AddSample(ms);
                    _log.Info($"[Keepalive] ping {ms:F2}ms");
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _status.IncrementErrors();
                    _log.Warn($"[Keepalive] error: {ex.Message}");
                }
#pragma warning restore CA1031 // Do not catch general exception types
            }

            _log.Info("Background keepalive stopped.");
        }, CancellationToken.None);
    }

    /// <summary>Stops the keepalive (idempotent).</summary>
    public void Stop()
    {
        CancellationTokenSource? cts = Interlocked.Exchange(ref _cts, null);
#pragma warning disable CA1031 // Do not catch general exception types
        try { cts?.Cancel(); }
        catch { /* ignore */ }
#pragma warning restore CA1031 // Do not catch general exception types
        finally { cts?.Dispose(); }
    }

    public async ValueTask<IDisposable> EnterPingScopeAsync(CancellationToken ct = default)
    {
        await _pingGate.WaitAsync(ct).ConfigureAwait(false);
        return new PingGateToken(_pingGate);
    }

    public IDisposable PauseScope()
    {
        _ = Interlocked.Increment(ref _pauseCount);
        return new PauseToken(this);
    }

    public void Dispose()
    {
        this.Stop();
        _pingGate.Dispose();
    }

    private sealed class PauseToken : IDisposable
    {
        private KeepAliveService? _owner;

        public PauseToken(KeepAliveService owner) => _owner = owner;

        public void Dispose()
        {
            KeepAliveService? owner = Interlocked.Exchange(ref _owner, null);
            if (owner is not null)
            {
                _ = Interlocked.Decrement(ref owner._pauseCount);
            }
        }
    }

    private sealed class PingGateToken(SemaphoreSlim gate) : IDisposable
    {
#pragma warning disable CA2213 // Gate owner is KeepAliveService; token only releases it.
        private SemaphoreSlim? _gate = gate;
#pragma warning restore CA2213

        public void Dispose()
        {
            SemaphoreSlim? gate = Interlocked.Exchange(ref _gate, null);
            if (gate is not null)
            {
                _ = gate.Release();
            }
        }
    }
}

