// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Extensions.Logging;
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
            // Try creating an IPv6 socket with DualMode first.
            // DualMode = true -> 1 socket that receives both IPv6 and IPv4-mapped (::ffff:x.x.x.x).
            // WHY prioritizes IPv6: Future-proof, supporting IPv4 clients via dual-stack.
            Socket? sock = null;

            try
            {
                sock = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp)
                {
                    Blocking = true,

                    // DualMode MUST be set BEFORE Bind — after Bind, it cannot be set again.
                    DualMode = s_config.DualMode,

                    // ExclusiveAddressUse = !ReuseAddress:
                    // ReuseAddress = true -> multiple processes can bind to the same port (load balancing).
                    // ReuseAddress = false -> exclusive -> prevent port hijacking.
                    ExclusiveAddressUse = !s_config.ReuseAddress,

                    // LingerState(false, 0) -> When Close() is called, RST is sent immediately,
                    // Don't wait for the drain buffer. WHY: Server-side listener does not require a liner
                    // Only per-connection sockets need to be considered.
                    LingerState = new LingerOption(false, 0)
                };

                // ReuseAddress MUST be set BEFORE Bind.
                // WHY: Allows binding the port again immediately after the server restart (avoid "Address already in use").
                sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, s_config.ReuseAddress ? 1 : 0);

                // Increase the receiver buffer of the listener socket.
                // WHY: Listener socket receives connection request (SYN), larger buffer
                // This helps OS queue have more pending connections before app accepts.
                sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, s_config.BufferSize);

                // IPv6Any (::) -> listens on all IPv6 interfaces (and IPv4 via DualMode).
                IPEndPoint epV6Any = new(IPAddress.IPv6Any, _port);

                s_logger?.Debug($"[NW.{nameof(TcpListenerBase)}:{nameof(Initialize)}] config-bind {epV6Any}.v6)");

                sock.Bind(epV6Any);
                sock.Listen(s_config.Backlog);

                _listener = sock;
                s_logger?.Debug($"[NW.{nameof(TcpListenerBase)}:{nameof(Initialize)}] config-listen {_listener.LocalEndPoint}.dual");

                return;
            }
            catch (Exception ex) when (Common.Exceptions.ExceptionClassifier.IsNonFatal(ex))
            {
                // IPv6/DualMode is not supported on this environment -> IPv4 fallback.
                // WHY not rethrow: Failover automatically is better than crashing the server.
                s_logger?.Warn($"[NW.{nameof(TcpListenerBase)}:{nameof(Initialize)}] failed-bind ex={ex.Message}");

                try
                {
                    sock?.Close();
                }
                catch (ObjectDisposedException closeEx)
                {
                    s_logger?.Debug(
                        $"[NW.{nameof(TcpListenerBase)}:{nameof(Initialize)}] " +
                        $"ipv6-fallback-close-ignored reason={closeEx.GetType().Name}");
                }
                catch (Exception closeEx) when (Common.Exceptions.ExceptionClassifier.IsNonFatal(closeEx))
                {
                    s_logger?.Warn(
                        $"[NW.{nameof(TcpListenerBase)}:{nameof(Initialize)}] " +
                        $"ipv6-fallback-close-failed", closeEx);
                }

                try
                {
                    sock?.Dispose();
                }
                catch (Exception disposeEx) when (Common.Exceptions.ExceptionClassifier.IsNonFatal(disposeEx))
                {
                    s_logger?.Warn(
                        $"[NW.{nameof(TcpListenerBase)}:{nameof(Initialize)}] " +
                        $"ipv6-fallback-dispose-failed", disposeEx);
                }

                sock = null;
            }
        }

        // Fallback: IPv4-only socket.
        // Used when: EnableIPv6 = false, or IPv6 bind fails.
        _listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
        {
            Blocking = true,
            ExclusiveAddressUse = !s_config.ReuseAddress,
            LingerState = new LingerOption(false, 0)
        };

        _listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, s_config.ReuseAddress ? 1 : 0);

        _listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, s_config.BufferSize);

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
    protected void InitializeOptions(Socket socket)
    {
        ArgumentNullException.ThrowIfNull(socket, nameof(socket));

        // When you want to disconnect immediately without making sure the data has been sent.
        // socket.LingerState = new LingerOption(true, NetworkSocketOptions.False);

        // Keep the socket in blocking mode.
        // WHY: Task-based async I/O works well with socket blocking.
        // Non-blocking mode requires handling WouldBlock errors in every recv/send call ->, which is much more complex.
        socket.Blocking = true;

        // OS-level buffer for each connection.
        // Larger -> fewer syscalls when throughput is high (batching more recv/send into the OS buffer).
        // Smaller -> saves memory when there are multiple connections simultaneously.
        socket.NoDelay = s_config.NoDelay;
        socket.SendBufferSize = s_config.BufferSize;
        socket.ReceiveBufferSize = s_config.BufferSize;

        if (s_config.KeepAlive)
        {
            // Enable TCP Keep-Alive -> OS will automatically send probes when connection idle.
            // WHY requires Keep-Alive: NAT/firewall usually drops the "silent" connection after a few minutes.
            // Keep-Alive keeps the connection alive and detects that the peer is dead (network failure).
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

            try
            {
                // Cross-platform API (.NET 5+): Windows, Linux, and macOS all support it.
                // Time = 3s: After 3 seconds of idle, start sending the first probe.
                socket.SetSocketOption(SocketOptionLevel.Tcp,
                                       SocketOptionName.TcpKeepAliveTime, 3);

                // Interval = 1s: If no response is given, send the next probe after 1 second.
                socket.SetSocketOption(SocketOptionLevel.Tcp,
                                       SocketOptionName.TcpKeepAliveInterval, 1);

                // RetryCount = 3: after 3 probes, there is no response -> connection dead -> close socket.
                // Total time to detect dead connection: 3 + (3 × 1) = 6 seconds.
                socket.SetSocketOption(SocketOptionLevel.Tcp,
                                       SocketOptionName.TcpKeepAliveRetryCount, 3);
            }
            catch (Exception ex) when (Common.Exceptions.ExceptionClassifier.IsNonFatal(ex))
            {
                // Fallback Windows-only: SIO_KEEPALIVE_VALS IOControl.
                // WHY fallback: Older runtime or restricted environment does not support cross-platform API.
                // SIO_KEEPALIVE_VALS = 12-byte struct: [on(4 bytes)][time_ms(4 bytes)][interval_ms(4 bytes)].
                if (OperatingSystem.IsWindows())
                {
                    const int on = 1;
                    const int time = 3_000; // 3 seconds = 3000ms
                    const int interval = 1_000; // 1 second = 1000ms

                    byte[] vals = new byte[12];
                    // WHY BinaryPrimitives instead of BitConverter: BinaryPrimitives does not allocate,
                    // Write directly to the buffer. LittleEndian because the Windows API requires it.
                    System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(MemoryExtensions.AsSpan(vals)[0..4], on);
                    System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(MemoryExtensions.AsSpan(vals)[4..8], time);
                    System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(MemoryExtensions.AsSpan(vals)[8..12], interval);
                    _ = socket.IOControl(IOControlCode.KeepAliveValues, vals, null);
                }
                // Non-Windows without support cross-platform API -> ignore silently.
                // WHY not throw: Best-effort; Keep-Alive will still work without it.
            }
        }
    }

    // These SocketError occur when the listener is shutting down normally:
    // Shutdown -> socket.Shutdown() is called.
    // TimedOut -> accept timeout (if a socket timeout is set).
    // NotSocket -> The socket was closed before accepting.
    // WouldBlock -> non-blocking socket without pending connection.
    // Interrupted -> accept is interrupted by signal/close.
    // InvalidArgument -> invalid sockets args (usually after Close).
    // OperationAborted -> async operation is destroyed (usually when Dispose).
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsIgnorableAcceptError(
        SocketError code,
        CancellationToken token)
        => token.IsCancellationRequested || code
        is SocketError.Shutdown
        or SocketError.TimedOut
        or SocketError.NotSocket
        or SocketError.WouldBlock
        or SocketError.Interrupted
        or SocketError.InvalidArgument
        or SocketError.OperationAborted;
}
