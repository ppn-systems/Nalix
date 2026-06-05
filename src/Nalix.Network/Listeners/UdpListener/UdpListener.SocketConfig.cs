// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions.Diagnostics;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;


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
        AddressFamily af = _options.EnableIPv6 ? AddressFamily.InterNetworkV6 : AddressFamily.InterNetwork;
        IPAddress bindAddress = _options.EnableIPv6 ? IPAddress.IPv6Any : IPAddress.Any;

        _socket = new Socket(af, SocketType.Dgram, ProtocolType.Udp);

        // IPv6 dual-mode allows the socket to accept both IPv4 and IPv6 datagrams
        // on a single binding when the OS supports it.
        if (af == AddressFamily.InterNetworkV6 && _options.DualMode)
        {
            try { _socket.DualMode = true; }
            catch (Exception ex) when (ex is SocketException or NotSupportedException or ObjectDisposedException or InvalidOperationException)
            {
                if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Debug)) { DiagnosticsEvents.Source.Write(DiagnosticsEvents.Internal.Debug, new DiagnosticLog("NW.UdpListenerBase:Initialize", $"dualmode-not-applied port={_port} exception-type={ex.GetType().Name}", ex)); }
            }
        }

        // Apply socket-level tuning before binding.
        this.ConfigureSocket(_socket);

        _socket.Bind(new IPEndPoint(bindAddress, _port));

        // Update the reusable endpoint to match the bound address family so that
        // ReceiveFromAsync can populate it without an address-family mismatch.
        _anyEndPoint = new IPEndPoint(bindAddress, 0);

        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Debug)) { DiagnosticsEvents.Source.Write(DiagnosticsEvents.Internal.Debug, new DiagnosticLog("NW.UdpListenerBase:Initialize", $"init-ok port={_port} af={af} options-reuse-address={_options.ReuseAddress} options-buffer-size={_options.BufferSize}")); }
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
        socket.ExclusiveAddressUse = !_options.ReuseAddress;
        socket.SendBufferSize = _options.BufferSize;
        socket.ReceiveBufferSize = _options.BufferSize;

        if (_options.ReuseAddress)
        {
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        }

        // 1. CHUNG: T?t phân m?nh gói tin (IP Fragmentation). 
        // Các UDP Protocol th?i gian th?c (Enterprise) luôn gi? size < MTU (1400 bytes).
        // N?u gói tin vô tình tru?t qua ngu?ng này, thà b? Drop nguyên c?c còn hon d? HÐH 
        // c?t nh? ra, làm tang d? tr? (latency spikes) do ch? gom n?i ghép (Reassembly).
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

        // 2. WINDOWS: S?a l?i WSAECONNRESET kinh di?n c?a UDP trên Windows.
        // Bình thu?ng n?u Server g?i tr? 1 gói UDP cho IP Client, nhung Client dã t?t m?ng (ngu?n không t?i), 
        // Windows s? nh?n du?c ICMP Port Unreachable. Ngay l?p t?c nó ném 1 l?i SocketException(ConnectionReset)
        // vào th?ng hàm ReceiveFromAsync G?N NH?T. Làm s?p ho?c gián do?n lu?ng nh?n c?a bao ngu?i khác!
        // SIO_UDP_CONNRESET = -1744830452 vô hi?u hóa co ch? báo l?i v? v?n này.
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
                if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Error)) { DiagnosticsEvents.Source.Write(DiagnosticsEvents.Internal.Error, new DiagnosticLog("NW.UdpListenerBase:ConfigureSocket", "Failed to set SIO_UDP_CONNRESET.", ex)); }
            }
        }
    }
}

