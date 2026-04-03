using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames;
using Nalix.SDK.Options;
using Nalix.SDK.Tools.Models;
using Nalix.SDK.Transport;

namespace Nalix.SDK.Tools.Services;

/// <summary>
/// Wraps <see cref="TcpSession"/> for MVVM-friendly application usage.
/// </summary>
public sealed class TcpClientService : ITcpClientService
{
    private readonly IPacketCatalogService _catalogService;
    private TcpSession? _session;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="TcpClientService"/> class.
    /// </summary>
    /// <param name="catalogService">The packet catalog service.</param>
    public TcpClientService(IPacketCatalogService catalogService) => _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));

    /// <inheritdoc/>
    public bool IsConnected => _session?.IsConnected == true;

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

        TcpSession session = new(options, _catalogService.Catalog.Registry);
        session.OnConnected += this.HandleConnected;
        session.OnDisconnected += this.HandleDisconnected;
        session.OnError += this.HandleError;
        session.OnMessageAsync += this.HandleMessageAsync;
        _session = session;

        await session.ConnectAsync(settings.Host, settings.Port, cancellationToken).ConfigureAwait(false);
        this.RaiseStatus($"Connected to {settings.Host}:{settings.Port}");
    }

    /// <inheritdoc/>
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        TcpSession? session = _session;
        if (session is null)
        {
            return;
        }

        _session = null;
        session.OnConnected -= this.HandleConnected;
        session.OnDisconnected -= this.HandleDisconnected;
        session.OnError -= this.HandleError;
        session.OnMessageAsync -= this.HandleMessageAsync;

        await session.DisconnectAsync().ConfigureAwait(false);
        session.Dispose();
        this.RaiseStatus("Disconnected");
    }

    /// <inheritdoc/>
    public async Task SendPacketAsync(IPacket packet, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(packet);

        if (_session is null)
        {
            throw new InvalidOperationException("The TCP session is not connected.");
        }

        PacketSnapshot snapshot = PacketSnapshot.FromPacket(packet);
        await _session.SendAsync(packet, cancellationToken).ConfigureAwait(false);

        PacketLogEntry entry = new()
        {
            Timestamp = DateTimeOffset.Now,
            Direction = "Sent",
            PacketName = packet.GetType().Name,
            Snapshot = snapshot
        };

        this.Dispatch(() => this.PacketSent?.Invoke(this, entry));
        this.RaiseStatus($"Sent {packet.GetType().Name} (0x{packet.OpCode:X4})");
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

    private void HandleConnected(object? sender, EventArgs e) => this.RaiseStatus("TCP connection established.");

    private void HandleDisconnected(object? sender, Exception exception) => this.RaiseStatus($"Disconnected: {exception.Message}");

    private void HandleError(object? sender, Exception exception) => this.RaiseStatus($"TCP error: {exception.Message}");

    private Task HandleMessageAsync(ReadOnlyMemory<byte> payload)
    {
        byte[] rawBytes = payload.ToArray();
        PacketLogEntry entry;

        try
        {
            FrameBase frame = _catalogService.Deserialize(rawBytes);
            entry = new PacketLogEntry
            {
                Timestamp = DateTimeOffset.Now,
                Direction = "Received",
                PacketName = frame.GetType().Name,
                Snapshot = PacketSnapshot.FromPacket(frame)
            };
        }
        catch (Exception exception)
        {
            entry = new PacketLogEntry
            {
                Timestamp = DateTimeOffset.Now,
                Direction = "Received",
                PacketName = "Unknown Packet",
                Snapshot = PacketCatalogService.CreateSnapshotFromRaw(rawBytes),
                DecodeStatus = exception.Message
            };
        }

        this.Dispatch(() => this.PacketReceived?.Invoke(this, entry));
        return Task.CompletedTask;
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
