// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Concurrency;
using Nalix.Common.Diagnostics.Abstractions;
using Nalix.Common.Networking.Packets.Abstractions;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Framework.Options;
using Nalix.Framework.Random;
using Nalix.Framework.Tasks;
using Nalix.SDK.Configuration;
using Nalix.SDK.Transport.Internal;
using Nalix.Shared.Memory.Buffers;

namespace Nalix.SDK.Transport;

/// <summary>
/// A reliable TCP client that delegates framing, send, and receive responsibilities to internal helpers.
/// Supports automatic reconnection, keep-alive heartbeats, and bandwidth rate sampling.
/// </summary>
public sealed class TcpSession : BaseTcpSession
{
    #region Constants and Static Fields

    /// <inheritdoc/>
    public const System.Byte HeaderSize = 2;

    internal static new readonly ILogger? Logging;
    internal static readonly IPacketRegistry Catalog;

    static TcpSession()
    {
        Logging = InstanceManager.Instance.GetExistingInstance<ILogger>();
        Catalog = InstanceManager.Instance.GetExistingInstance<IPacketRegistry>()
            ?? throw new System.InvalidOperationException("IPacketRegistry instance not found.");

        BufferConfig bufferConfig = ConfigurationManager.Instance.Get<BufferConfig>();

        bufferConfig.TotalBuffers = 32;
        bufferConfig.EnableMemoryTrimming = true;
        bufferConfig.TrimIntervalMinutes = 2;
        bufferConfig.DeepTrimIntervalMinutes = 10;
        bufferConfig.EnableAnalytics = false;
        bufferConfig.AdaptiveGrowthFactor = 1.25;
        bufferConfig.MaxMemoryPercentage = 0.05;
        bufferConfig.SecureClear = false;
        bufferConfig.EnableQueueCompaction = false;
        bufferConfig.AutoTuneOperationThreshold = 32;
        bufferConfig.FallbackToArrayPool = true;
        bufferConfig.ExpandThresholdPercent = 0.20;
        bufferConfig.ShrinkThresholdPercent = 0.60;
        bufferConfig.MinimumIncrease = 1;
        bufferConfig.MaxBufferIncreaseLimit = 16;
        bufferConfig.BufferAllocations = "256,0.25; 512,0.30; 1024,0.45";
        bufferConfig.MaxMemoryBytes = 0;

        bufferConfig.Validate();
    }

    #endregion Constants and Static Fields

    #region Fields

    private IWorkerHandle? _receiveHandle;

    private System.String? _host;
    private System.UInt16? _port;

    private System.Int32 _reconnecting = 0;
    private System.Int32 _hasEverConnected = 0;

    internal System.Int64 _bytesSent = 0;
    internal System.Int64 _bytesReceived = 0;

    // Per-interval counters reset by RATE_SAMPLER_TICK
    internal System.Int64? _lastSampleTick = 0;
    internal System.Int64 _sendCounterForInterval = 0;
    internal System.Int64 _receiveCounterForInterval = 0;

    // Last computed bandwidth samples (bytes/s)
    internal System.Int64 _lastSendBps = 0;
    internal System.Int64 _lastReceiveBps = 0;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the total number of bytes sent since connection.
    /// </summary>
    public System.Int64 BytesSent => System.Threading.Interlocked.Read(ref _bytesSent);

    /// <summary>
    /// Gets the total number of bytes received since connection.
    /// </summary>
    public System.Int64 BytesReceived => System.Threading.Interlocked.Read(ref _bytesReceived);

    /// <summary>
    /// Gets the average send bandwidth in bytes per second over the last sample interval.
    /// </summary>
    public System.Int64 SendBytesPerSecond => System.Threading.Interlocked.Read(ref _lastSendBps);

    /// <summary>
    /// Gets the average receive bandwidth in bytes per second over the last sample interval.
    /// </summary>
    public System.Int64 ReceiveBytesPerSecond => System.Threading.Interlocked.Read(ref _lastReceiveBps);

    #endregion Properties


    /// <inheritdoc/>
    public event System.EventHandler<System.Int32>? OnReconnected;

    /// <inheritdoc/>
    public TcpSession() : base()
    {
        Options = ConfigurationManager.Instance.Get<TransportOptions>();
        Options.Validate();

        if (Catalog is null)
        {
            throw new System.InvalidOperationException("Missing IPacketRegistry");
        }
    }

    /// <inheritdoc/>
    protected override void CreateFrameHelpers()
    {
        _sender = new FRAME_SENDER(GET_CONNECTED_SOCKET_OR_THROW, Options, REPORT_BYTES_SENT, HANDLE_SEND_ERROR);
        _receiver = new FRAME_READER(GET_CONNECTED_SOCKET_OR_THROW, Options, HANDLE_RECEIVE_MESSAGE, HANDLE_RECEIVE_ERROR, REPORT_BYTES_RECEIVED);
    }

    /// <inheritdoc/>
    protected override void StartReceiveWorker(System.Threading.CancellationToken loopToken)
    {
        if (_receiver is null)
        {
            return;
        }

        try
        {
            _receiveHandle = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().ScheduleWorker(
                name: $"TcpSession-Receive-{_host}:{_port}",
                group: "client",
                work: async (_, workerCt) =>
                {
                    var effective = workerCt.CanBeCanceled ? workerCt : loopToken;
                    await _receiver.ReceiveLoopAsync(effective).ConfigureAwait(false);
                },
                options: new WorkerOptions { CancellationToken = loopToken }
            );
        }
        catch
        {
            _ = System.Threading.Tasks.Task.Run(() => _receiver.ReceiveLoopAsync(loopToken), loopToken);
        }
    }

    /// <inheritdoc/>
    public override async System.Threading.Tasks.Task ConnectAsync(System.String? host = null, System.UInt16? port = null, System.Threading.CancellationToken ct = default)
    {
        System.ObjectDisposedException.ThrowIf(System.Threading.Volatile.Read(ref _disposed) == 1, nameof(TcpSession));

        System.String? effectiveHost = System.String.IsNullOrWhiteSpace(host) ? Options.Address : host;
        System.UInt16 effectivePort = port ?? Options.Port;

        if (System.String.IsNullOrWhiteSpace(effectiveHost))
        {
            throw new System.ArgumentException("Host required");
        }

        if (IsConnected &&
            System.String.Equals(_host, effectiveHost, System.StringComparison.OrdinalIgnoreCase) &&
            _port == effectivePort)
        {
            return;
        }

        if (IsConnected)
        {
            CLEANUP_CONNECTION();
        }

        lock (_sync)
        {
            if (_loopCts is not null)
            {
                CANCEL_AND_DISPOSE_LOCKED(ref _loopCts);
            }
        }

        using var connectCts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(ct);

        if (Options.ConnectTimeoutMillis > 0)
        {
            connectCts.CancelAfter(Options.ConnectTimeoutMillis);
        }

        System.Exception? lastEx = null;

        var addrs = await System.Net.Dns.GetHostAddressesAsync(effectiveHost, connectCts.Token);

        foreach (var addr in addrs)
        {
            var s = new System.Net.Sockets.Socket(addr.AddressFamily, System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);

            try
            {
                s.NoDelay = Options.NoDelay;
                s.SendBufferSize = Options.BufferSize;
                s.ReceiveBufferSize = Options.BufferSize;

                await s.ConnectAsync(new System.Net.IPEndPoint(addr, effectivePort), connectCts.Token);

                System.Threading.CancellationToken loopToken;

                lock (_sync)
                {
                    _socket = s;
                    _loopCts = new System.Threading.CancellationTokenSource();
                    loopToken = _loopCts.Token;

                    _host = effectiveHost;
                    _port = effectivePort;
                }

                CreateFrameHelpers();

                System.Boolean isReconnect = System.Threading.Interlocked.Exchange(ref _hasEverConnected, 1) == 1;

                if (isReconnect)
                {
                    OnReconnected?.Invoke(this, 0);
                }
                else
                {
                    RaiseConnected();
                }

                StartReceiveWorker(loopToken);
                System.Threading.Interlocked.Exchange(ref _reconnecting, 0);

                return;
            }
            catch (System.Exception ex)
            {
                lastEx = ex;
                try { s.Dispose(); } catch { }
            }
        }

        throw lastEx ?? new System.Net.Sockets.SocketException((System.Int32)System.Net.Sockets.SocketError.HostNotFound);
    }

    /// <inheritdoc/>
    protected override void REPORT_BYTES_SENT(System.Int32 count)
    {
        System.Threading.Interlocked.Add(ref _bytesSent, count);
        base.REPORT_BYTES_SENT(count);
    }

    /// <inheritdoc/>
    protected override void REPORT_BYTES_RECEIVED(System.Int32 count)
    {
        System.Threading.Interlocked.Add(ref _bytesReceived, count);
        base.REPORT_BYTES_RECEIVED(count);
    }

    /// <inheritdoc/>
    protected override void HANDLE_SEND_ERROR(System.Exception ex)
    {
        RaiseError(ex);
        TriggerReconnect(ex);
    }

    /// <inheritdoc/>
    protected override void HANDLE_RECEIVE_ERROR(System.Exception ex)
    {
        RaiseError(ex);
        TriggerReconnect(ex);
    }

    private void TriggerReconnect(System.Exception ex)
    {
        if (System.Threading.Interlocked.CompareExchange(ref _reconnecting, 1, 0) == 0)
        {
            _ = HANDLE_DISCONNECT_AND_RECONNECT_ASYNC(ex);
        }
    }

    /// <inheritdoc/>
    protected override void CLEANUP_CONNECTION()
    {
        System.Boolean wasConnected = IsConnected;

        base.CLEANUP_CONNECTION();

        try
        {
            if (_receiveHandle != null)
            {
                InstanceManager.Instance.GetOrCreateInstance<TaskManager>()
                    .CancelWorker(_receiveHandle.Id);

                _receiveHandle = null;
            }
        }
        catch { }

        if (wasConnected)
        {
            RaiseDisconnected(new System.Exception("Disconnected"));
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1163:Unused parameter", Justification = "<Pending>")]
    internal async System.Threading.Tasks.Task HANDLE_DISCONNECT_AND_RECONNECT_ASYNC(System.Exception cause)
    {
        CLEANUP_CONNECTION();

        if (!Options.ReconnectEnabled || System.Threading.Volatile.Read(ref _disposed) == 1)
        {
            return;
        }

        if (System.String.IsNullOrEmpty(_host) || _port == 0)
        {
            return;
        }

        System.Int32 attempt = 0;
        System.Int64 max = System.Math.Max(1, Options.ReconnectMaxDelayMillis);
        System.Int64 delay = System.Math.Max(1, Options.ReconnectBaseDelayMillis);

        while (System.Threading.Volatile.Read(ref _disposed) == 0 &&
               (Options.ReconnectMaxAttempts == 0 || attempt < Options.ReconnectMaxAttempts))
        {
            attempt++;

            System.Int64 jitter = (System.Int64)(Csprng.NextDouble() * delay * 0.3);
            await System.Threading.Tasks.Task.Delay((System.Int32)System.Math.Min(delay + jitter, System.Int32.MaxValue));

            try
            {
                await ConnectAsync(_host, _port);
                OnReconnected?.Invoke(this, attempt);
                return;
            }
            catch
            {
                delay = System.Math.Min(max, delay * 2);
            }
        }
    }
}