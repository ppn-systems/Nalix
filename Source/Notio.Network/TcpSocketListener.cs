using Notio.Network.Config;
using Notio.Network.Session;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Network;

/// <summary>
/// Nghe kết nối socket trên một địa chỉ và cổng cụ thể.
/// </summary>
public class TcpSocketListener : IDisposable
{
    private bool _disposed;
    private Socket? _listenerSocket;
    private readonly IPEndPoint _endPoint;
    private readonly SocketConfig _config;
    private readonly SecurityConfig? _security;
    private readonly SessionManager _clientManager;
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    /// <summary>
    /// Cho biết liệu bộ nghe có đang chạy hay không.
    /// </summary>
    public bool IsRunning => _listenerSocket != null && _listenerSocket.IsBound;

    /// <summary>
    /// Cho biết liệu kết nối có bảo mật hay không.
    /// </summary>
    public bool IsSecure => _security?.ServerCertificate != null;

    /// <summary>
    /// Số lượng khách hàng hiện đang kết nối.
    /// </summary>
    public int ConnectedClientsCount => _clientManager.ConnectedClientsCount;

    /// <summary>
    /// Danh sách các khách hàng hiện đang kết nối.
    /// </summary>
    public IReadOnlyDictionary<string, SocketSession> Clients => _clientManager.Clients;

    /// <summary>
    /// Sự kiện được kích hoạt khi một khách hàng mới kết nối.
    /// </summary>
    public event EventHandler<SocketSession>? ClientConnected
    {
        add => _clientManager.ClientConnected += value;
        remove => _clientManager.ClientConnected -= value;
    }

    /// <summary>
    /// Sự kiện được kích hoạt khi một khách hàng ngắt kết nối.
    /// </summary>
    public event EventHandler<SocketSession>? ClientDisconnected
    {
        add => _clientManager.ClientDisconnected += value;
        remove => _clientManager.ClientDisconnected -= value;
    }

    /// <summary>
    /// Sự kiện được kích hoạt khi dữ liệu được nhận từ một khách hàng.
    /// </summary>
    public event EventHandler<(SocketSession Client, byte[] Data)>? DataReceived
    {
        add => _clientManager.DataReceived += value;
        remove => _clientManager.DataReceived -= value;
    }

    /// <summary>
    /// Sự kiện được kích hoạt khi xảy ra lỗi.
    /// </summary>
    public event EventHandler<Exception>? ErrorOccurred;

    /// <summary>
    /// Khởi tạo một <see cref="TcpSocketListener"/> mới.
    /// </summary>
    /// <param name="endPoint">Điểm cuối để nghe kết nối.</param>
    /// <param name="config">Cấu hình socket.</param>
    /// <param name="security">Cấu hình bảo mật.</param>
    public TcpSocketListener(
        IPEndPoint endPoint,
        SocketConfig? config = null,
        SecurityConfig? security = null)
    {
        _endPoint = endPoint;
        _config = config ?? new SocketConfig();
        _security = security;
        _clientManager = new SessionManager(_config);
        _clientManager.ErrorOccurred += OnError;
    }

    private void OnError(object? sender, Exception ex)
    {
        ErrorOccurred?.Invoke(this, ex);
    }

    /// <summary>
    /// Bắt đầu nghe kết nối socket một cách không đồng bộ.
    /// </summary>
    public async Task StartAsync()
    {
        if (IsRunning)
            throw new InvalidOperationException("Server đã đang chạy.");

        _listenerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        ConfigureSocket(_listenerSocket);

        try
        {
            _listenerSocket.Bind(_endPoint);
            _listenerSocket.Listen(_config.Backlog);
            await AcceptClientsAsync();
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, ex);
            await StopAsync();
            throw;
        }
    }

    private void ConfigureSocket(Socket socket)
    {
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, _config.ReuseAddress);
        socket.NoDelay = _config.NoDelay;
        socket.ReceiveBufferSize = _config.ReceiveBufferSize;
        socket.SendBufferSize = _config.SendBufferSize;

        if (OperatingSystem.IsWindows())
        {
            byte[] keepAlive = new byte[12];
            BitConverter.GetBytes((uint)1).CopyTo(keepAlive, 0);
            BitConverter.GetBytes((uint)(_config.KeepAliveInterval.TotalMilliseconds)).CopyTo(keepAlive, 4);
            BitConverter.GetBytes((uint)1000).CopyTo(keepAlive, 8);
            socket.IOControl(IOControlCode.KeepAliveValues, keepAlive, null);
        }
    }

    private async Task AcceptClientsAsync()
    {
        while (!_cancellationTokenSource.Token.IsCancellationRequested && IsRunning)
        {
            try
            {
                var clientSocket = await Task.Factory.FromAsync(
                    _listenerSocket!.BeginAccept,
                    _listenerSocket!.EndAccept,
                    null);

                this.ConfigureSocket(clientSocket);
                await _clientManager.AddClientAsync(clientSocket, _security);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                ErrorOccurred?.Invoke(this, ex);
            }
        }
    }

    /// <summary>
    /// Gửi dữ liệu tới tất cả các khách hàng kết nối, ngoại trừ một khách hàng cụ thể nếu được cung cấp.
    /// </summary>
    /// <param name="data">Dữ liệu để gửi.</param>
    /// <param name="excludeClientId">ID của khách hàng không nhận dữ liệu.</param>
    public async Task BroadcastAsync(byte[] data, string? excludeClientId = null)
    {
        await _clientManager.BroadcastAsync(data, excludeClientId);
    }

    /// <summary>
    /// Dừng nghe kết nối socket một cách không đồng bộ.
    /// </summary>
    public async Task StopAsync()
    {
        if (!IsRunning) return;

        try
        {
            _cancellationTokenSource.Cancel();
            await _clientManager.DisconnectAllAsync();
            _listenerSocket?.Close();
            _listenerSocket = null;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, ex);
        }
    }

    /// <summary>
    /// Giải phóng tài nguyên được sử dụng bởi <see cref="TcpSocketListener"/>.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Giải phóng tài nguyên được sử dụng bởi <see cref="TcpSocketListener"/>.
    /// </summary>
    /// <param name="disposing">True để giải phóng tài nguyên quản lý, False để chỉ giải phóng tài nguyên không quản lý.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                StopAsync().Wait();
                _cancellationTokenSource.Dispose();
                _clientManager.Dispose();
                _listenerSocket?.Dispose();
            }
            _disposed = true;
        }
    }

    /// <summary>
    /// Giải phóng tài nguyên khi hủy đối tượng.
    /// </summary>
    ~TcpSocketListener()
    {
        Dispose(false);
    }
}