// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Extensions.Logging;
using Nalix.Common.Exceptions;
using Nalix.Common.Networking;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Network.Configurations;
using Nalix.Network.Timekeeping;

namespace Nalix.Network.Listeners.Udp;

public abstract partial class UdpListenerBase
{
    #region Constants

    /// <summary>
    /// Milliseconds
    /// </summary>
    private const int ReceiveTimeout = 500;

    #endregion Constants

    #region Fields

    private static readonly NetworkSocketOptions s_config;

    private readonly ushort _port;
    private readonly IProtocol _protocol;
    private readonly SemaphoreSlim _lock;

    private UdpClient? _udpClient;
    private CancellationTokenSource? _cts;
    private CancellationToken _cancellationToken;

    private int _isDisposed;
    private volatile bool _isRunning;

    /// <summary>
    /// Diagnostics fields
    /// </summary>
    private long _rxPackets;
    private long _rxBytes;
    private long _dropShort;
    private long _dropUnauth;
    private long _dropUnknown;

    private long _recvErrors;

    /// <summary>
    /// Time sync diagnostics
    /// </summary>
    private long _lastSyncUnixMs;
    private long _lastDriftMs;
    private int _procSeq = -1;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets a value indicating whether the UDP listener is currently running and listening for datagrams.
    /// </summary>
    public bool IsListening => _isRunning;


    /// <summary>
    /// Gets or sets a value indicating whether time synchronization is enabled for the UDP listener.
    /// Throws <see cref="InternalErrorException"/> if set while the listener is running.
    /// </summary>
    /// <exception cref="InternalErrorException"></exception>
    public bool IsTimeSyncEnabled
    {
        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => InstanceManager.Instance.GetOrCreateInstance<TimeSynchronizer>().IsTimeSyncEnabled;

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            if (_isRunning)
            {
                throw new InternalErrorException($"[{nameof(UdpListenerBase)}] Cannot change IsTimeSyncEnabled while listening.");
            }

            InstanceManager.Instance.GetOrCreateInstance<TimeSynchronizer>()
                           .IsTimeSyncEnabled = value;

            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Info($"[{nameof(UdpListenerBase)}] timesync={value}");
        }
    }

    #endregion Properties

    #region Constructors

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static UdpListenerBase()
    {
        s_config = ConfigurationManager.Instance.Get<NetworkSocketOptions>();
        s_config.Validate();
    }


    /// <summary>
    /// Initializes a new instance of the <see cref="UdpListenerBase"/> class with the specified port, protocol, buffer pool, and logger.
    /// </summary>
    /// <param name="port">The UDP port to listen on.</param>
    /// <param name="protocol">The protocol handler for processing datagrams.</param>
    [DebuggerStepThrough]
    protected UdpListenerBase(ushort port, IProtocol protocol)
    {
        ArgumentNullException.ThrowIfNull(protocol, nameof(protocol));

        _port = port;
        _protocol = protocol;

        _lock = new SemaphoreSlim(1, 1);

        InstanceManager.Instance.GetOrCreateInstance<TimeSynchronizer>()
                       .TimeSynchronized += this.SynchronizeTime;

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[NW.{nameof(UdpListenerBase)}] created port={_port} protocol={protocol.GetType().Name}");
    }


    /// <summary>
    /// Initializes a new instance of the <see cref="UdpListenerBase"/> class using the configured port, protocol, buffer pool, and logger.
    /// </summary>
    /// <param name="protocol">The protocol handler for processing datagrams.</param>
    [DebuggerStepThrough]
    protected UdpListenerBase(IProtocol protocol)
        : this(s_config.Port, protocol)
    {
    }

    #endregion Constructors

    #region IDisposable

    /// <inheritdoc/>
    [DebuggerStepThrough]
    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    [DebuggerStepThrough]
    protected virtual void Dispose(bool disposing)
    {
        // Atomic check-and-set: 0 -> 1
        if (Interlocked.CompareExchange(ref _isDisposed, 1, 0) != 0)
        {
            return;
        }

        if (disposing)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            _cancellationToken = default;

            try
            {
                _udpClient?.Close();
                _udpClient = null;

                InstanceManager.Instance.GetOrCreateInstance<TimeSynchronizer>()
                               .TimeSynchronized -= this.SynchronizeTime;
            }
            catch { }

            _lock.Dispose();
        }

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Info($"[NW.{nameof(UdpListenerBase)}:{nameof(Dispose)}] disposed");
    }

    #endregion IDisposable
}
