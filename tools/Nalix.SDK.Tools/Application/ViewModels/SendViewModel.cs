// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Networking.Protocols;
using Nalix.Framework.Injection;
using Nalix.SDK.Transport;
using Nalix.Shared.Frames;
using Nalix.Shared.Frames.Controls;
using System.ComponentModel;

namespace Application.ViewModels;

/// <summary>
/// ViewModel cho tab Send Data. Cho phép nhập dữ liệu FrameBase, validate và gửi.
/// Hỗ trợ gửi Control, Directive, Handshake.
/// </summary>
public class SendViewModel(ConnectionViewModel connVm) : INotifyPropertyChanged
{
    public ConnectionViewModel ConnectionVM { get; } = connVm;

    // FrameData: input dạng chuỗi, thường là JSON, text hoặc XML tuỳ UI.
    public String FrameData
    {
        get => _frameData;
        set
        {
            if (_frameData != value)
            {
                _frameData = value;
                OnPropertyChanged(nameof(FrameData));
            }
        }
    }
    private String _frameData = "";

    /// <summary>
    /// Lựa chọn loại packet (Control, Directive, Handshake).
    /// </summary>
    public PacketType SelectedPacketType
    {
        get => _selectedPacketType;
        set
        {
            if (_selectedPacketType != value)
            {
                _selectedPacketType = value;
                OnPropertyChanged(nameof(SelectedPacketType));
            }
        }
    }
    private PacketType _selectedPacketType = PacketType.Handshake;

    /// <summary>
    /// Báo lỗi input cho UI.
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
    private String _errorMessage = "";

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Kiểm tra input hợp lệ, parse được packet.
    /// </summary>
    public Boolean ValidateInput()
    {
        ErrorMessage = "";

        if (String.IsNullOrWhiteSpace(FrameData))
        {
            ErrorMessage = "FrameData không được để trống (FrameBase input cannot be empty)";
            return false;
        }

        FrameBase? frame = CreateFrameFromInput(FrameData, SelectedPacketType);
        if (frame == null)
        {
            ErrorMessage = "Dữ liệu không hợp lệ hoặc không thể parse (Data invalid or parse failed)";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Gửi packet ra kênh TCP thực tế.
    /// </summary>
    public async void SendFrame()
    {
        ErrorMessage = "";

        if (!ConnectionVM.IsConnected)
        {
            ErrorMessage = "Bạn chưa kết nối server (Not connected)";
            return;
        }

        FrameBase? frame = CreateFrameFromInput(FrameData, SelectedPacketType);

        if (frame == null)
        {
            ErrorMessage = "Dữ liệu không hợp lệ hoặc không thể parse (Data invalid or parse failed)";
            return;
        }

        try
        {
            // Lấy đối tượng TcpSession từ ConnectionVM
            TcpSession tcpSession = InstanceManager.Instance.GetOrCreateInstance<TcpSession>();
            if (!tcpSession.IsConnected)
            {
                ErrorMessage = "TCP chưa kết nối (TCP session not connected)";
                return;
            }

            // Gửi frame theo loại frame
            await tcpSession.SendAsync(frame);

            ErrorMessage = ""; // Thành công!
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Gửi lỗi: {ex.Message}";
        }
    }

    /// <summary>
    /// Parse FrameData và tạo đúng loại packet theo lựa chọn SelectedPacketType
    /// </summary>
    private FrameBase? CreateFrameFromInput(String input, PacketType type)
    {
        try
        {
            // Có thể dùng Json, XML, hoặc parse tuỳ cấu trúc.
            return type switch
            {
                PacketType.Control => ParseControlPacket(input),// Dummy parse, bạn nên thay bằng: Control.Deserialize(input) hoặc logic parse thực tế
                PacketType.Directive => ParseDirectivePacket(input),
                PacketType.Handshake => ParseHandshakePacket(input),
                _ => null,
            };
        }
        catch
        {
            return null;
        }
    }

    // Dummy parse - bạn hãy thay bằng logic thực tế (JSON/XML/deserializer)
    private Nalix.Shared.Frames.Controls.Control ParseControlPacket(String input)
    {
        // Dùng logic thực tế: có thể là JSON, hoặc khởi tạo bằng input trực tiếp
        // Ví dụ bên dưới là mẫu khởi tạo mặc định, bạn cần sửa để phù hợp với UI của bạn
        var pkt = new Nalix.Shared.Frames.Controls.Control();
        pkt.Initialize(ControlType.NONE);
        return pkt;
    }
    private Directive ParseDirectivePacket(String input)
    {
        var pkt = new Directive();
        pkt.Initialize(
            ControlType.NONE,
            ProtocolReason.NONE,
            ProtocolAdvice.NONE,
            sequenceId: 0
        );
        return pkt;
    }
    private Handshake ParseHandshakePacket(String input)
    {
        var pkt = new Handshake();
        pkt.Initialize(Array.Empty<Byte>(), ProtocolType.TCP); // Chèn input thật vào Data nếu cần
        return pkt;
    }

    protected void OnPropertyChanged(String propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>
/// Các loại packet hỗ trợ gửi trong UI.
/// </summary>
public enum PacketType
{
    Control,
    Directive,
    Handshake
}
