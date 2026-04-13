// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Nalix.Common.Abstractions;
using Nalix.Common.Exceptions;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Framework.Identifiers;
using Nalix.SDK.Options;
using Nalix.SDK.Tools.Abstractions;
using Nalix.SDK.Tools.Models;
using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Extensions;

namespace Nalix.SDK.Tools.Services;

/// <summary>
/// Wraps <see cref="TransportSession"/> implementations for MVVM-friendly application usage.
/// </summary>
public sealed class NetworkClientService : INetworkClientService
{
    private readonly IPacketCatalogService _catalogService;
    private readonly IAppConfigurationService _configurationService;
    private TransportSession? _session;
    private bool _disposed;
    private bool _autoPingEnabled = true;
    private CancellationTokenSource? _pingCts;
    private byte[] _savedSecret = [];
    private Snowflake _savedSessionToken = Snowflake.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="NetworkClientService"/> class.
    /// </summary>
    /// <param name="catalogService">The packet catalog service.</param>
    /// <param name="configurationService">The app configuration service.</param>
    public NetworkClientService(IPacketCatalogService catalogService, IAppConfigurationService configurationService)
    {
        _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
    }

    /// <inheritdoc/>
    public bool IsConnected => _session?.IsConnected == true;

    /// <inheritdoc/>
    public ProtocolType Transport => _session switch
    {
        TcpSession => ProtocolType.TCP,
        UdpSession => ProtocolType.UDP,
        _ => ProtocolType.NONE
    };

    /// <inheritdoc/>
    public Snowflake SessionToken => _session?.Options.SessionToken ?? Snowflake.Empty;

    /// <inheritdoc/>
    public event EventHandler<string>? StatusChanged;

    /// <inheritdoc/>
    public event EventHandler<PacketLogEntry>? PacketSent;

    /// <inheritdoc/>
    public event EventHandler<PacketLogEntry>? PacketReceived;

    /// <inheritdoc/>
    public bool AutoPingEnabled
    {
        get => _autoPingEnabled;
        set
        {
            if (_autoPingEnabled == value)
            {
                return;
            }

            _autoPingEnabled = value;
            if (value)
            {
                this.StartPingLoop();
            }
            else
            {
                this.StopPingLoop();
            }
        }
    }

    /// <inheritdoc/>
    public async Task ConnectAsync(ConnectionSettings settings, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(settings);

        // Capture state from previous session if available BEFORE disconnecting
        if (_session is not null)
        {
            _savedSecret = [.. _session.Options.Secret];
            _savedSessionToken = _session.Options.SessionToken;
        }

        await this.DisconnectAsync(cancellationToken).ConfigureAwait(false);

        TransportOptions options = new()
        {
            Address = settings.Host,
            Port = settings.Port,
            NoDelay = true,
            CompressionEnabled = false,
            EncryptionEnabled = false,
            Secret = _savedSecret,
            SessionToken = _savedSessionToken
        };

        // If we are connecting to a DIFFERENT host/port, we should probably clear the saved state
        // but for now let's just use what's in settings if it matches.
        if (settings.Transport == ProtocolType.UDP && Snowflake.TryParse(settings.SessionToken, out Snowflake manualToken))
        {
            options.SessionToken = manualToken;
        }

        _session = settings.Transport == ProtocolType.UDP
            ? new UdpSession(options, _catalogService.Catalog.Registry)
            : new TcpSession(options, _catalogService.Catalog.Registry);

        _session.OnMessageReceived += this.HandleMessageReceived;
        _session.OnError += (s, e) => this.StatusChanged?.Invoke(this, $"Error: {e.Message}");
        _session.OnDisconnected += (s, e) =>
        {
            this.StatusChanged?.Invoke(this, "Disconnected.");
        };

        await _session.ConnectAsync(settings.Host, settings.Port, cancellationToken).ConfigureAwait(false);

        if (_session is UdpSession udpRef && Snowflake.TryParse(settings.SessionToken, out Snowflake token))
        {
            udpRef.SessionToken = token;
        }

        this.StartPingLoop();

        this.RaiseStatus(string.Format(
            CultureInfo.CurrentCulture,
            _configurationService.Texts.StatusConnectedFormat,
            settings.Host,
            settings.Port));
    }

    /// <inheritdoc/>
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        TransportSession? session = _session;
        if (session is null)
        {
            return;
        }

        _session = null;
        session.OnMessageReceived -= this.HandleMessageReceived;

        this.StopPingLoop();

        await session.DisconnectAsync().ConfigureAwait(false);
        session.Dispose();
        this.RaiseStatus(_configurationService.Texts.StatusDisconnected);
    }

    /// <inheritdoc/>
    public async Task SendPacketAsync(IPacket packet, bool? encrypt = null, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(packet);

        if (_session is null)
        {
            throw new InvalidOperationException(_configurationService.Texts.StatusTcpSessionNotConnected);
        }

        if (_session is TcpSession tcpSession)
        {
            await tcpSession.SendAsync(packet, encrypt, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await _session.SendAsync(packet, cancellationToken).ConfigureAwait(false);
        }

        this.HandlePacketSent(packet);

        this.RaiseStatus(string.Format(CultureInfo.CurrentCulture, _configurationService.Texts.StatusPacketSentFormat, packet.GetType().Name, packet.OpCode));
    }

    private void HandlePacketSent(IPacket packet)
    {
        PacketSnapshot snapshot = PacketSnapshot.FromPacket(packet);
        DateTimeOffset timestamp = DateTimeOffset.Now;

        PacketLogEntry entry = new()
        {
            Timestamp = timestamp,
            PacketName = packet.GetType().Name,
            Snapshot = snapshot,
            Summary = string.Format(
                CultureInfo.CurrentCulture,
                _configurationService.Texts.HistorySummaryFormat,
                timestamp,
                snapshot.OpCode,
                packet.GetType().Name,
                snapshot.RawBytes.Length)
        };

        this.Dispatch(() => this.PacketSent?.Invoke(this, entry));
    }

    /// <inheritdoc/>
    public async Task HandshakeAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_session is null)
        {
            throw new InvalidOperationException(_configurationService.Texts.StatusTcpSessionNotConnected);
        }

        if (_session is TcpSession tcpSession)
        {
            await tcpSession.HandshakeAsync(cancellationToken).ConfigureAwait(false);

            // Persist the newly established session state
            _savedSecret = [.. tcpSession.Options.Secret];
            _savedSessionToken = tcpSession.Options.SessionToken;
        }
        else
        {
            throw new NotSupportedException("Cryptographic handshake is currently only supported for TCP transport in this tool.");
        }
    }

    /// <inheritdoc/>
    public async Task ResumeAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_session is null)
        {
            throw new InvalidOperationException(_configurationService.Texts.StatusTcpSessionNotConnected);
        }

        if (_session is TcpSession tcpSession)
        {
            if (tcpSession.Options.SessionToken.IsEmpty || tcpSession.Options.Secret.Length == 0)
            {
                throw new NetworkException("No valid session state (token/secret) available to resume. Please perform a handshake first.");
            }

            bool success = await tcpSession.ResumeSessionAsync(cancellationToken).ConfigureAwait(false);
            if (success)
            {
                // Update persisted token if server assigned a new one during resume
                _savedSessionToken = tcpSession.Options.SessionToken;
                this.RaiseStatus(_configurationService.Texts.StatusResumeSuccess);
            }
            else
            {
                throw new NetworkException("The server rejected the session resume request (session may have expired).");
            }
        }
        else
        {
            throw new NotSupportedException("Session resume is current only supported for TCP transport in this tool.");
        }
    }

    /// <inheritdoc/>
    public async Task<double> PingAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_session is null)
        {
            throw new InvalidOperationException(_configurationService.Texts.StatusTcpSessionNotConnected);
        }

        if (_session is TcpSession tcpSession)
        {
            return await tcpSession.PingAsync(5000, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            throw new NotSupportedException("Ping is currently only supported for TCP transport in this tool.");
        }
    }

    private void StartPingLoop()
    {
        if (!_autoPingEnabled || _session is not TcpSession || _pingCts is not null)
        {
            return;
        }

        _pingCts = new CancellationTokenSource();
        _ = Task.Run(() => this.PingLoopAsync(_pingCts.Token));
    }

    private void StopPingLoop()
    {
        _pingCts?.Cancel();
        _pingCts?.Dispose();
        _pingCts = null;
    }

    private async Task PingLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), ct).ConfigureAwait(false);

                if (_session is TcpSession tcpSession && tcpSession.IsConnected)
                {
                    double rtt = await tcpSession.PingAsync(5000, ct).ConfigureAwait(false);
                    this.RaiseStatus(string.Format(CultureInfo.CurrentCulture, _configurationService.Texts.StatusPingSuccessFormat, rtt));
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                this.RaiseStatus(string.Format(CultureInfo.CurrentCulture, _configurationService.Texts.StatusPingFailedFormat, ex.Message));
                await Task.Delay(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_session is not null)
        {
            try
            {
                _ = _session.DisconnectAsync();
            }
            catch
            {
                // Ignore disposal failures during shutdown.
            }

            _session.Dispose();
            _session = null;
        }
    }

    private void HandleMessageReceived(object? sender, IBufferLease lease)
    {
        byte[] rawBytes = lease.Memory.ToArray();
        PacketLogEntry entry;
        DateTimeOffset timestamp = DateTimeOffset.Now;

        try
        {
            IPacket packet = _catalogService.Deserialize(rawBytes);
            PacketSnapshot snapshot = PacketSnapshot.FromPacket(packet);
            entry = new PacketLogEntry
            {
                Timestamp = timestamp,
                PacketName = packet.GetType().Name,
                Snapshot = snapshot,
                Summary = string.Format(
                    CultureInfo.CurrentCulture,
                    _configurationService.Texts.HistorySummaryFormat,
                    timestamp,
                    snapshot.OpCode,
                    packet.GetType().Name,
                    snapshot.RawBytes.Length)
            };
        }
        catch (Exception exception)
        {
            PacketSnapshot snapshot = PacketCatalogService.CreateSnapshotFromRaw(rawBytes);
            entry = new PacketLogEntry
            {
                Timestamp = timestamp,
                PacketName = _configurationService.Texts.UnknownPacketName,
                Snapshot = snapshot,
                DecodeStatus = exception.Message,
                Summary = string.Format(
                    CultureInfo.CurrentCulture,
                    _configurationService.Texts.HistorySummaryFormat,
                    timestamp,
                    snapshot.OpCode,
                    _configurationService.Texts.UnknownPacketName,
                    snapshot.RawBytes.Length)
            };
        }

        this.Dispatch(() => this.PacketReceived?.Invoke(this, entry));
    }

    private void RaiseStatus(string message) => this.Dispatch(() => this.StatusChanged?.Invoke(this, message));

    private void Dispatch(Action action)
    {
        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(action);
            return;
        }

        action();
    }
}
