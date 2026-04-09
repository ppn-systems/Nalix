// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Nalix.Common.Abstractions;
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
    public async Task ConnectAsync(ConnectionSettings settings, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(settings);

        await this.DisconnectAsync(cancellationToken).ConfigureAwait(false);

        TransportOptions options = new()
        {
            Address = settings.Host,
            Port = settings.Port,
            NoDelay = true,
            CompressionEnabled = false,
            EncryptionEnabled = false
        };

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

        await session.DisconnectAsync().ConfigureAwait(false);
        session.Dispose();
        this.RaiseStatus(_configurationService.Texts.StatusDisconnected);
    }

    /// <inheritdoc/>
    public async Task SendPacketAsync(IPacket packet, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(packet);

        if (_session is null)
        {
            throw new InvalidOperationException(_configurationService.Texts.StatusTcpSessionNotConnected);
        }

        PacketSnapshot snapshot = PacketSnapshot.FromPacket(packet);
        DateTimeOffset timestamp = DateTimeOffset.Now;
        await _session.SendAsync(packet, cancellationToken).ConfigureAwait(false);

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
        this.RaiseStatus(string.Format(CultureInfo.CurrentCulture, _configurationService.Texts.StatusPacketSentFormat, packet.GetType().Name, packet.OpCode));
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
        }
        else
        {
            throw new NotSupportedException("Cryptographic handshake is currently only supported for TCP transport in this tool.");
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
