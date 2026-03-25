// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using Nalix.Common.Networking;

namespace Nalix.Network.Listeners.Tcp;

public abstract partial class TcpListenerBase
{
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Initialize()
    {
        if (s_config.EnableIPv6)
        {
            // Try IPv6 + DualMode first
            Socket sock = null;
            try
            {
                sock = new Socket(
                    AddressFamily.InterNetworkV6,
                    SocketType.Stream, ProtocolType.Tcp)
                {
                    Blocking = true,
                    DualMode = s_config.DualMode,                 // Must set before Bind
                    ExclusiveAddressUse = !s_config.ReuseAddress, // fast rebind combo with ReuseAddress
                    LingerState = new LingerOption(false, 0)
                };

                // Reuse BEFORE bind
                sock.SetSocketOption(
                    SocketOptionLevel.Socket,
                    SocketOptionName.ReuseAddress, s_config.ReuseAddress ? 1 : 0);

                // Optional: larger listen buffer (per-connection tuning is more important)
                sock.SetSocketOption(
                    SocketOptionLevel.Socket,
                    SocketOptionName.ReceiveBuffer, s_config.BufferSize);

                IPEndPoint epV6Any = new(IPAddress.IPv6Any, _port);

                s_logger?.Debug($"[NW.{nameof(TcpListenerBase)}:{nameof(Initialize)}] config-bind {epV6Any}.v6)");

                sock.Bind(epV6Any);
                sock.Listen(s_config.Backlog);

                _listener = sock;
                s_logger?.Debug($"[NW.{nameof(TcpListenerBase)}:{nameof(Initialize)}] config-listen {_listener.LocalEndPoint}.dual");

                return;
            }
            catch (Exception ex)
            {
                s_logger?.Warn($"[NW.{nameof(TcpListenerBase)}:{nameof(Initialize)}] failed-bind ex={ex.Message}");

                // Clean up the half-initialized IPv6 socket before falling back
                try
                {
                    sock?.Close();
                }
                catch { }
                try
                {
                    sock?.Dispose();
                }
                catch { }

                sock = null;
            }
        }

        // Fallback: IPv4-only
        _listener = new Socket(
            AddressFamily.InterNetwork,
            SocketType.Stream, ProtocolType.Tcp)
        {
            Blocking = true,
            ExclusiveAddressUse = !s_config.ReuseAddress,
            LingerState = new LingerOption(false, 0)
        };

        _listener.SetSocketOption(
            SocketOptionLevel.Socket,
            SocketOptionName.ReuseAddress, s_config.ReuseAddress ? 1 : 0);

        _listener.SetSocketOption(
            SocketOptionLevel.Socket,
            SocketOptionName.ReceiveBuffer, s_config.BufferSize);

        IPEndPoint epV4Any = new(IPAddress.Any, _port);

        s_logger?.Debug($"[NW.{nameof(TcpListenerBase)}:{nameof(Initialize)}] config-bind {epV4Any}.v4");

        _listener.Bind(epV4Any);
        _listener.Listen(s_config.Backlog);

        s_logger?.Debug($"[NW.{nameof(TcpListenerBase)}:{nameof(Initialize)}] config-listen {_listener.LocalEndPoint}");
    }

    /// <summary>
    /// Applies per-connection socket options to an accepted client socket.
    /// </summary>
    /// <param name="socket">
    /// The accepted client socket to configure. Must not be <see langword="null"/>.
    /// </param>
    /// <remarks>
    /// <para>
    /// Called by <see cref="InitializeConnection"/> immediately after a socket is accepted,
    /// before the <see cref="IConnection"/> wrapper is constructed. Options applied here
    /// affect only the individual client socket — not the listener socket itself.
    /// </para>
    /// <para>
    /// The following options are always applied:
    /// <list type="bullet">
    ///   <item>
    ///     <term><c>Blocking = true</c></term>
    ///     <description>
    ///     Keeps the socket in blocking mode. Task-based async I/O works correctly with
    ///     blocking sockets; forcing non-blocking mode here would require all receive/send
    ///     loops to handle <see cref="SocketError.WouldBlock"/> explicitly.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term><c>NoDelay</c></term>
    ///     <description>
    ///     Controls Nagle's algorithm. Set to <see langword="true"/> to disable batching and
    ///     reduce latency (recommended for interactive or real-time protocols).
    ///     Driven by <c>s_config.NoDelay</c>.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term><c>SendBufferSize</c> / <c>ReceiveBufferSize</c></term>
    ///     <description>
    ///     Sets the OS-level socket send and receive buffers to <c>s_config.BufferSize</c>.
    ///     Larger values reduce syscall frequency under high throughput at the cost of memory.
    ///     </description>
    ///   </item>
    /// </list>
    /// </para>
    /// <para>
    /// When <c>s_config.KeepAlive</c> is <see langword="true"/>, TCP keep-alive probing is
    /// enabled with the following timings:
    /// <list type="bullet">
    ///   <item>
    ///     <term>Keep-alive time</term>
    ///     <description>3 seconds — idle time before the first probe is sent.</description>
    ///   </item>
    ///   <item>
    ///     <term>Keep-alive interval</term>
    ///     <description>1 second — time between subsequent probes.</description>
    ///   </item>
    ///   <item>
    ///     <term>Keep-alive retry count</term>
    ///     <description>3 probes — after which the connection is considered dead.</description>
    ///   </item>
    /// </list>
    /// The cross-platform <c>TcpKeepAliveTime</c> / <c>TcpKeepAliveInterval</c> /
    /// <c>TcpKeepAliveRetryCount</c> socket options are attempted first (available on
    /// .NET 5+ across Windows, Linux, and macOS). If that call fails — typically on older
    /// runtimes or restricted environments — the method falls back to the Windows-only
    /// <c>SIO_KEEPALIVE_VALS</c> IOControl, which packs the same three values into a
    /// 12-byte little-endian struct sent via
    /// <see cref="Socket.IOControl(IOControlCode, byte[], byte[])"/>.
    /// The fallback is silently skipped on non-Windows platforms.
    /// </para>
    /// <para>
    /// Subclasses may call <c>base.InitializeOptions(socket)</c> and then apply additional
    /// socket options for specialized transports (for example, TLS timeout tuning or
    /// protocol-specific buffer sizing).
    /// </para>
    /// </remarks>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining)]
    [SuppressMessage(
        "CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
    [SuppressMessage(
        "Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]
    protected static void InitializeOptions([NotNull] Socket socket)
    {
        // When you want to disconnect immediately without making sure the data has been sent.
        // socket.LingerState = new LingerOption(true, NetworkSocketOptions.False);

        // Keep the accepted socket in blocking mode; Task-based async works fine with blocking sockets.
        // If you really want non-blocking I/O, ensure your Accept/Receive loops expect WouldBlock.
        socket.Blocking = true;

        // Performance tuning
        socket.NoDelay = s_config.NoDelay;
        socket.SendBufferSize = s_config.BufferSize;
        socket.ReceiveBufferSize = s_config.BufferSize;

        if (s_config.KeepAlive)
        {
            // Windows specific settings
            socket.SetSocketOption(SocketOptionLevel.Socket,
                                   SocketOptionName.KeepAlive, true);

            try
            {
                // Cross-platform in modern .NET
                socket.SetSocketOption(SocketOptionLevel.Tcp,
                                       SocketOptionName.TcpKeepAliveTime, 3);

                socket.SetSocketOption(SocketOptionLevel.Tcp,
                                       SocketOptionName.TcpKeepAliveInterval, 1);

                socket.SetSocketOption(SocketOptionLevel.Tcp,
                                       SocketOptionName.TcpKeepAliveRetryCount, 3);
            }
            catch
            {
                // Fallback Windows-only SIO_KEEPALIVE_VALS if needed
                if (OperatingSystem.IsWindows())
                {
                    // Win32 SIO_KEEPALIVE_VALS: [on(4)][time(4 ms)][interval(4 ms)]
                    // 1. Turning on Keep-Alive
                    // 2. 3 seconds without data, send Keep-Alive
                    // 3. Send every 1 second if there is no response

                    const int on = 1;
                    const int time = 3_000;
                    const int interval = 1_000;

                    byte[] vals = new byte[12];
                    System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(MemoryExtensions.AsSpan(vals)[0..4], on);
                    System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(MemoryExtensions.AsSpan(vals)[4..8], time);
                    System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(MemoryExtensions.AsSpan(vals)[8..12], interval);
                    _ = socket.IOControl(IOControlCode.KeepAliveValues, vals, null);
                }
            }
        }
    }

    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: NotNull]
    private static bool IsIgnorableAcceptError(
        [NotNull] SocketError code,
        [NotNull] CancellationToken token)
        => token.IsCancellationRequested || code
        is SocketError.Shutdown
        or SocketError.TimedOut
        or SocketError.NotSocket
        or SocketError.WouldBlock
        or SocketError.Interrupted
        or SocketError.InvalidArgument
        or SocketError.OperationAborted;
}
