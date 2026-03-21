// Copyright (c) 2026 phcnguyen. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Application.Models;
using Nalix.Framework.Injection;
using Nalix.SDK.Transport;
using System.ComponentModel;

namespace Application.ViewModels;

/// <summary>
/// ViewModel cho phần Connection.
/// Dùng mô hình MVVM, hỗ trợ binding cho UI.
/// Thực hiện bảo mật, chuẩn hóa và validate input.
/// </summary>
public class ConnectionViewModel : INotifyPropertyChanged
{
    #region Fields

    private Boolean _isConnected;
    private String _errorMessage = String.Empty;
    private TcpSession? _tcpSession;
    private CancellationTokenSource? _connectCts;

    #endregion Fields

    /// <summary>
    /// Cấu hình kết nối (binding từ UI).
    /// </summary>
    public ConnectionSettings Settings { get; set; } = new();

    /// <summary>
    /// Trạng thái đã kết nối thành công chưa.
    /// </summary>
    public Boolean IsConnected
    {
        get => _isConnected;
        private set
        {
            if (_isConnected != value)
            {
                _isConnected = value;
                OnPropertyChanged(nameof(IsConnected));
            }
        }
    }

    /// <summary>
    /// Thông báo lỗi input cho UI.
    /// </summary>
    public String ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (_errorMessage != value)
            {
                _errorMessage = value;
                OnPropertyChanged(nameof(ErrorMessage));
            }
        }
    }

    /// <summary>
    /// Event PropertyChanged cho binding.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Thực hiện kết nối tới server bằng TcpSession.
    /// Validate đầu vào, báo lỗi đúng chuẩn, không chặn UI thread.
    /// </summary>
    public async Task ConnectAsync()
    {
        // Validate settings trước
        if (!Settings.IsValid(out String error))
        {
            ErrorMessage = error;
            IsConnected = false;
            return;
        }

        // Ngắt kết nối cũ nếu có
        if (_tcpSession != null)
        {
            Disconnect();
        }

        ErrorMessage = String.Empty;
        IsConnected = false;

        _tcpSession = InstanceManager.Instance.GetOrCreateInstance<TcpSession>();
        _connectCts = new CancellationTokenSource();

        try
        {
            await _tcpSession.ConnectAsync(Settings.Host, (UInt16?)Settings.Port, _connectCts.Token);

            // Kiểm tra thực sự đã kết nối
            IsConnected = _tcpSession.IsConnected;
            ErrorMessage = String.Empty;

            // Bạn có thể lắng nghe sự kiện OnReconnected nếu cần:
            // _tcpSession.OnReconnected += (sender, attempt) => { ... };
        }
        catch (Exception ex)
        {
            IsConnected = false;
            ErrorMessage = ex.Message;
            // Nếu cần log bảo mật, log thêm ex.ToString() vào file bảo mật
        }
    }

    /// <summary>
    /// Ngắt kết nối và giải phóng tài nguyên.
    /// </summary>
    public void Disconnect()
    {
        _tcpSession?.Dispose();
        _tcpSession = null;
        _connectCts?.Cancel();
        _connectCts = null;
        IsConnected = false;
        ErrorMessage = String.Empty;
    }

    /// <summary>
    /// Raise PropertyChanged event.
    /// </summary>
    /// <param name="propertyName">Tên property thay đổi</param>
    protected virtual void OnPropertyChanged(String propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}