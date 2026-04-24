// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace Nalix.Network.Listeners.Udp;

public abstract partial class UdpListenerBase
{
    /// <summary>
    /// Creates and binds the underlying <see cref="Socket"/> for UDP datagram reception.
    /// </summary>
    /// <remarks>
    /// Derived types can override this method to customize how the UDP socket is created or bound,
    /// but should preserve the contract that <see cref="_socket"/> is ready for receive operations
    /// when the method returns.
    /// </remarks>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining)]
    protected virtual void Initialize()
    {
        // Determine address family from configuration.
        AddressFamily af = s_options.EnableIPv6 ? AddressFamily.InterNetworkV6 : AddressFamily.InterNetwork;
        IPAddress bindAddress = s_options.EnableIPv6 ? IPAddress.IPv6Any : IPAddress.Any;

        _socket = new Socket(af, SocketType.Dgram, ProtocolType.Udp);

        // IPv6 dual-mode allows the socket to accept both IPv4 and IPv6 datagrams
        // on a single binding when the OS supports it.
        if (af == AddressFamily.InterNetworkV6 && s_options.DualMode)
        {
            try { _socket.DualMode = true; }
            catch (Exception ex) when (ex is SocketException or NotSupportedException or ObjectDisposedException or InvalidOperationException)
            {
                s_logger?.Debug(
                    $"[NW.{nameof(UdpListenerBase)}:{nameof(Initialize)}] " +
                    $"dualmode-not-applied port={_port} reason={ex.GetType().Name}");
            }
        }

        // Apply socket-level tuning before binding.
        this.ConfigureSocket(_socket);

        _socket.Bind(new IPEndPoint(bindAddress, _port));

        // Update the reusable endpoint to match the bound address family so that
        // ReceiveFromAsync can populate it without an address-family mismatch.
        _anyEndPoint = new IPEndPoint(bindAddress, 0);

        s_logger?.Debug(
            $"[NW.{nameof(UdpListenerBase)}:{nameof(Initialize)}] " +
            $"init-ok port={_port} af={af} reuse={s_options.ReuseAddress} buf={s_options.BufferSize}");
    }

    /// <summary>
    /// Applies UDP-relevant socket-level performance tuning to the given socket.
    /// </summary>
    /// <param name="socket">The socket to configure.</param>
    /// <remarks>
    /// Override this method when a derived listener needs a different tuning profile, such as
    /// platform-specific socket options or custom buffer sizing.
    /// <para>
    /// Note: TCP-specific options (<c>NoDelay</c>, <c>KeepAlive</c>) are intentionally excluded
    /// because they have no effect on UDP sockets.
    /// </para>
    /// </remarks>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining)]
    [SuppressMessage(
        "CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
    [SuppressMessage(
        "Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]
    protected virtual void ConfigureSocket(Socket socket)
    {
        ArgumentNullException.ThrowIfNull(socket, nameof(socket));

        socket.Blocking = false;
        socket.ExclusiveAddressUse = !s_options.ReuseAddress;
        socket.SendBufferSize = s_options.BufferSize;
        socket.ReceiveBufferSize = s_options.BufferSize;

        if (s_options.ReuseAddress)
        {
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        }

        // 1. CHUNG: Tắt phân mảnh gói tin (IP Fragmentation). 
        // Các UDP Protocol thời gian thực (Enterprise) luôn giữ size < MTU (1400 bytes).
        // Nếu gói tin vô tình trượt qua ngưỡng này, thà bị Drop nguyên cục còn hơn để HĐH 
        // cắt nhỏ ra, làm tăng độ trễ (latency spikes) do chờ gom nối ghép (Reassembly).
        try
        {
            socket.DontFragment = true;
        }
        catch (SocketException)
        {
            // Best effort only: some platforms/socket configurations do not support this option.
        }
        catch (ObjectDisposedException)
        {
            // Socket lifetime race: ignore to preserve existing non-fatal behavior.
        }

        // 2. WINDOWS: Sửa lỗi WSAECONNRESET kinh điển của UDP trên Windows.
        // Bình thường nếu Server gửi trả 1 gói UDP cho IP Client, nhưng Client đã tắt mạng (nguồn không tới), 
        // Windows sẽ nhận được ICMP Port Unreachable. Ngay lập tức nó ném 1 lỗi SocketException(ConnectionReset)
        // vào thẳng hàm ReceiveFromAsync GẦN NHẤT. Làm sập hoặc gián đoạn luồng nhận của bao người khác!
        // SIO_UDP_CONNRESET = -1744830452 vô hiệu hóa cơ chế báo lỗi vớ vẩn này.
        if (OperatingSystem.IsWindows())
        {
            try
            {
                // DWORD 0 = false -> disable UDP connection reset
                const int SIO_UDP_CONNRESET = -1744830452;
                _ = socket.IOControl(SIO_UDP_CONNRESET, [0, 0, 0, 0], null);
            }
            catch (SocketException ex)
            {
                s_logger?.Error(
                    "Failed to set SIO_UDP_CONNRESET. " +
                    "UDP sockets on Windows may throw SocketException(ConnectionReset) when receiving datagrams from unreachable clients.", ex);
            }
        }
    }
}
