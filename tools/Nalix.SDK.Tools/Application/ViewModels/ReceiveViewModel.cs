// Copyright (c) 2026 phcnguyen. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Shared;
using Nalix.Framework.Injection;
using Nalix.SDK.Transport;
using Nalix.Shared.Frames;
using Nalix.Shared.Frames.Controls;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Application.ViewModels;

/// <summary>
/// ViewModel cho tab Receive Data. Quản lý danh sách frame nhận được.
/// Hỗ trợ nhận từ OnMessageReceived theo dạng IBufferLease.
/// </summary>
public class ReceiveViewModel : INotifyPropertyChanged
{
    public ConnectionViewModel ConnectionVM { get; }

    public ObservableCollection<FrameBase> ReceivedFrames { get; } = [];

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
    private String _errorMessage = "";

    public event PropertyChangedEventHandler? PropertyChanged;

    public ReceiveViewModel(ConnectionViewModel connVm)
    {
        ConnectionVM = connVm;
        SubscribeTcpSession();
    }

    private void SubscribeTcpSession()
    {
        TcpSession? tcpSession = InstanceManager.Instance.GetOrCreateInstance<TcpSession>();
        tcpSession?.OnMessageReceived += TcpSession_OnMessageReceived;
    }

    private void TcpSession_OnMessageReceived(Object? sender, IBufferLease bufferLease)
    {
        try
        {
            // Lấy ra buffer từ lease
            var buffer = bufferLease.Span;

            FrameBase? frame = TryDeserializeFrame(buffer);

            if (frame != null)
            {
                // Đảm bảo thread UI nếu cần (WPF)
                // Application.Current.Dispatcher.Invoke(() => ReceivedFrames.Add(frame));
                ReceivedFrames.Add(frame);
            }
            else
            {
                ErrorMessage = "Không parse được dữ liệu nhận (Could not parse received data)";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Nhận dữ liệu lỗi: {ex.Message}";
        }
    }

    private static FrameBase? TryDeserializeFrame(System.ReadOnlySpan<Byte> buffer)
    {
        try
        {
            if (buffer.Length > 0)
            {
                try { return Nalix.Shared.Frames.Controls.Control.Deserialize(buffer); } catch { }
                try { return Directive.Deserialize(buffer); } catch { }
                try { return Handshake.Deserialize(buffer); } catch { }
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    public void ClearReceivedFrames()
    {
        ReceivedFrames.Clear();
        ErrorMessage = "";
    }

    protected void OnPropertyChanged(String propertyName)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}