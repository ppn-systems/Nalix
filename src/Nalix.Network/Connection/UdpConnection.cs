using Nalix.Common.Connection;
using Nalix.Common.Cryptography;
using Nalix.Common.Identity;
using Nalix.Common.Package;
using Nalix.Common.Security;
using Nalix.Identifiers;
using Nalix.Network.Connection.Transport;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Nalix.Network.Connection;

/// <summary>
/// Represents a UDP-based network connection implementing the IConnection interface.
/// </summary>
internal class UdpConnection(IPEndPoint remoteEndPoint) : IConnection
{
    private readonly UdpClient _udpClient = new();

    private readonly IPEndPoint _remoteEndPoint = remoteEndPoint
        ?? throw new ArgumentNullException(nameof(remoteEndPoint));

    private readonly IEncodedId _id = new Base36Id();
    private readonly TransportCache _cache = new();
    private bool _disposed;

    public IEncodedId Id => _id;

    public long UpTime => _cache.Uptime;

    public long LastPingTime => throw new NotSupportedException("Ping time is not supported in UDP connections.");

    public ReadOnlyMemory<byte> IncomingPacket
    {
        get
        {
            if (_cache.Incoming.TryGetValue(out System.ReadOnlyMemory<byte> data))
                return data;

            return System.ReadOnlyMemory<byte>.Empty; // Avoid returning null
        }
    }

    public string RemoteEndPoint => _remoteEndPoint.ToString();

    public DateTimeOffset Timestamp => _timestamp;

    public byte[] EncryptionKey { get; set; } = [];

    public PermissionLevel Level { get; set; } = PermissionLevel.None;

    public EncryptionType Encryption { get; set; } = EncryptionType.None;

    /// <inheritdoc/>
    public System.Collections.Generic.Dictionary<string, object> Metadata { get; } = [];

    public event EventHandler<IConnectEventArgs>? OnCloseEvent;

    public event EventHandler<IConnectEventArgs>? OnProcessEvent;

    public event EventHandler<IConnectEventArgs>? OnPostProcessEvent;

    /// <summary>
    /// Begin receiving UDP packets asynchronously until cancelled.
    /// </summary>
    public async void BeginReceive(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(UdpConnection));

        // Corrected the event subscription to use a lambda that calls the event handler
        _cache.PacketCached += () => OnProcessEvent?.Invoke(this, new ConnectionEventArgs(this));

        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                _cache.PushIncoming((await _udpClient.ReceiveAsync(cancellationToken)).Buffer);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation, ignore or log if needed
        }
        catch (Exception ex)
        {
            // Handle receive errors
            throw new InvalidOperationException("Failed to receive UDP data.", ex);
        }
    }

    public void Close(bool force = false)
    {
        if (_disposed) return;

        try
        {
            _udpClient.Close();
            OnCloseEvent?.Invoke(this, new ConnectionEventArgs(this));
        }
        finally
        {
            _disposed = true;
        }
    }

    public void Disconnect(string? reason = null) => Close();

    public void Dispose()
    {
        this.Close();
        _udpClient.Dispose();
    }

    public bool Send(IPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ObjectDisposedException.ThrowIf(_disposed, nameof(UdpConnection));

        return Send(packet.Serialize().Span);
    }

    public bool Send(ReadOnlySpan<byte> message)
    {
        if (message.IsEmpty) return false;
        ObjectDisposedException.ThrowIf(_disposed, nameof(UdpConnection));

        int sentBytes = _udpClient.Send(message.ToArray(), message.Length, _remoteEndPoint);
        return sentBytes == message.Length;
    }

    public async Task<bool> SendAsync(IPacket packet, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ObjectDisposedException.ThrowIf(_disposed, nameof(UdpConnection));

        return await SendAsync(packet.Serialize(), cancellationToken);
    }

    public async Task<bool> SendAsync(ReadOnlyMemory<byte> message, CancellationToken cancellationToken = default)
    {
        if (message.IsEmpty) return false;
        ObjectDisposedException.ThrowIf(_disposed, nameof(UdpConnection));

        int sentBytes = await _udpClient.SendAsync(message.ToArray(), message.Length, _remoteEndPoint);
        return sentBytes == message.Length;
    }
}
