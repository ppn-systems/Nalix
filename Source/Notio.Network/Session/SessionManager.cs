using Notio.Network.Config;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Notio.Network.Session;

/// <summary>
/// Quản lý phiên làm việc của các khách hàng kết nối thông qua socket.
/// </summary>
public class SessionManager : IDisposable
{
    private readonly ConcurrentDictionary<string, SocketSession> _clients = new();
    private readonly SocketConfig _config;
    private bool _disposed;

    /// <summary>
    /// Sự kiện được kích hoạt khi một khách hàng mới kết nối.
    /// </summary>
    public event EventHandler<SocketSession>? ClientConnected;

    /// <summary>
    /// Sự kiện được kích hoạt khi một khách hàng ngắt kết nối.
    /// </summary>
    public event EventHandler<SocketSession>? ClientDisconnected;

    /// <summary>
    /// Sự kiện được kích hoạt khi dữ liệu được nhận từ một khách hàng.
    /// </summary>
    public event EventHandler<(SocketSession Client, byte[] Data)>? DataReceived;

    /// <summary>
    /// Sự kiện được kích hoạt khi xảy ra lỗi.
    /// </summary>
    public event EventHandler<Exception>? ErrorOccurred;

    /// <summary>
    /// Số lượng khách hàng hiện đang kết nối.
    /// </summary>
    public int ConnectedClientsCount => _clients.Count;

    /// <summary>
    /// Danh sách các khách hàng hiện đang kết nối.
    /// </summary>
    public IReadOnlyDictionary<string, SocketSession> Clients => _clients;

    /// <summary>
    /// Khởi tạo một <see cref="SessionManager"/> mới.
    /// </summary>
    /// <param name="config">Cấu hình socket.</param>
    public SessionManager(SocketConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// Thêm khách hàng mới vào phiên làm việc.
    /// </summary>
    /// <param name="socket">Socket của khách hàng mới.</param>
    /// <param name="security">Cấu hình bảo mật (tùy chọn).</param>
    public async Task AddClientAsync(Socket socket, SecurityConfig? security = null)
    {
        if (ConnectedClientsCount >= _config.MaxConnections)
        {
            socket.Close();
            return;
        }

        var client = new SocketSession(socket, _config.ReceiveBufferSize, security);
        client.DataReceived += OnClientDataReceived;
        client.Disconnected += OnClientDisconnected;
        client.ErrorOccurred += OnClientError;

        if (_clients.TryAdd(client.Id, client))
        {
            ClientConnected?.Invoke(this, client);
            await client.StartReceiving();
        }
        else
        {
            client.Disconnect();
            client.Dispose();
        }
    }

    private void OnClientDataReceived(object? sender, byte[] data)
    {
        if (sender is SocketSession client)
            DataReceived?.Invoke(this, (client, data));
    }

    private void OnClientDisconnected(object? sender, EventArgs e)
    {
        if (sender is SocketSession client)
        {
            if (_clients.TryRemove(client.Id, out _))
                ClientDisconnected?.Invoke(this, client);
        }
    }

    private void OnClientError(object? sender, Exception ex)
    {
        ErrorOccurred?.Invoke(this, ex);
    }

    /// <summary>
    /// Gửi dữ liệu tới tất cả các khách hàng kết nối, ngoại trừ một khách hàng cụ thể nếu được cung cấp.
    /// </summary>
    /// <param name="data">Dữ liệu để gửi.</param>
    /// <param name="excludeClientId">ID của khách hàng không nhận dữ liệu (tùy chọn).</param>
    public async Task BroadcastAsync(byte[] data, string? excludeClientId = null)
    {
        foreach (var client in _clients.Values)
        {
            if (client.Id != excludeClientId)
            {
                try
                {
                    await client.SendAsync(data);
                }
                catch (Exception ex)
                {
                    ErrorOccurred?.Invoke(this, ex);
                }
            }
        }
    }

    /// <summary>
    /// Ngắt kết nối tất cả các khách hàng.
    /// </summary>
    public async Task DisconnectAllAsync()
    {
        foreach (var client in _clients.Values)
        {
            try
            {
                client.Disconnect();
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
            }
        }
        _clients.Clear();
    }

    /// <summary>
    /// Giải phóng tài nguyên được sử dụng bởi <see cref="SessionManager"/>.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Giải phóng tài nguyên được sử dụng bởi <see cref="SessionManager"/>.
    /// </summary>
    /// <param name="disposing">True để giải phóng tài nguyên quản lý, False để chỉ giải phóng tài nguyên không quản lý.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                DisconnectAllAsync().Wait();
                foreach (var client in _clients.Values)
                {
                    client.Dispose();
                }
            }
            _disposed = true;
        }
    }

    /// <summary>
    /// Giải phóng tài nguyên khi hủy đối tượng.
    /// </summary>
    ~SessionManager()
    {
        Dispose(false);
    }
}