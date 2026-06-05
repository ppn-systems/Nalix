// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using Nalix.Abstractions.Diagnostics;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking;
using Nalix.Network.Internal.Pooling;

namespace Nalix.Network.Listeners.Tcp;

public abstract partial class TcpListenerBase
{
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "<Pending>")]
    private static readonly SocketOptionName ReusePortOption = (SocketOptionName)15;

    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Initialize()
    {
        if (_config.EnableIPv6)
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
                    DualMode = _config.DualMode,

                    // ExclusiveAddressUse = !ReuseAddress:
                    // ReuseAddress = true -> multiple processes can bind to the same port (load balancing).
                    // ReuseAddress = false -> exclusive -> prevent port hijacking.
                    ExclusiveAddressUse = !_config.ReuseAddress,

                    // LingerState(false, 0) -> When Close() is called, RST is sent immediately,
                    // Don't wait for the drain buffer. WHY: Server-side listener does not require a liner
                    // Only per-connection sockets need to be considered.
                    LingerState = new LingerOption(false, 0)
                };

                // ReuseAddress MUST be set BEFORE Bind.
                // WHY: Allows binding the port again immediately after the server restart (avoid "Address already in use").
                sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, _config.ReuseAddress ? 1 : 0);

                // Increase the receiver buffer of the listener socket.
                // WHY: Listener socket receives connection request (SYN), larger buffer
                // This helps OS queue have more pending connections before app accepts.
                sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, _config.BufferSize);

                // IPv6Any (::) -> listens on all IPv6 interfaces (and IPv4 via DualMode).
                IPEndPoint epV6Any = new(IPAddress.IPv6Any, _port);

                if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Debug))
                {
                    DiagnosticsEvents.Source.Write(DiagnosticsEvents.Internal.Debug, new DiagnosticLog("NW.TcpListenerBase:Initialize", $"config-bind ep-v6-any={epV6Any}"));
                }

                sock.Bind(epV6Any);
                sock.Listen(_config.Backlog);

                _listener = sock;
                if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Debug))
                {
                    DiagnosticsEvents.Source.Write(DiagnosticsEvents.Internal.Debug, new DiagnosticLog("NW.TcpListenerBase:Initialize", $"config-listen local-endpoint={_listener.LocalEndPoint}"));
                }

                return;
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                // IPv6/DualMode is not supported on this environment -> IPv4 fallback.
                // WHY not rethrow: Failover automatically is better than crashing the server.
                if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Warning))
                {
                    DiagnosticsEvents.Source.Write(DiagnosticsEvents.Internal.Warning, new DiagnosticLog("NW.TcpListenerBase:Initialize", "failed-bind", ex));
                }

                try
                {
                    sock?.Close();
                }
                catch (ObjectDisposedException closeEx)
                {
                    if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Debug))
                    {
                        DiagnosticsEvents.Source.Write(DiagnosticsEvents.Internal.Debug, new DiagnosticLog("NW.TcpListenerBase:Initialize", $"ipv6-fallback-close-ignored type={closeEx.GetType().Name}", ex));
                    }
                }
                catch (Exception closeEx) when (ExceptionClassifier.IsNonFatal(closeEx))
                {
                    if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Warning))
                    {
                        DiagnosticsEvents.Source.Write(DiagnosticsEvents.Internal.Warning, new DiagnosticLog("NW.TcpListenerBase:Initialize", "ipv6-fallback-close-failed", closeEx));
                    }
                }

                try
                {
                    sock?.Dispose();
                }
                catch (Exception disposeEx) when (ExceptionClassifier.IsNonFatal(disposeEx))
                {
                    if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Warning))
                    {
                        DiagnosticsEvents.Source.Write(DiagnosticsEvents.Internal.Warning, new DiagnosticLog("NW.TcpListenerBase:Initialize", "ipv6-fallback-dispose-failed", disposeEx));
                    }
                }
            }
        }

        // Fallback: IPv4-only socket.
        // Used when: EnableIPv6 = false, or IPv6 bind fails.
        _listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
        {
            Blocking = true,
            ExclusiveAddressUse = !_config.ReuseAddress,
            LingerState = new LingerOption(false, 0)
        };

        _listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, _config.ReuseAddress ? 1 : 0);

        _listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, _config.BufferSize);

        IPEndPoint epV4Any = new(IPAddress.Any, _port);

        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Debug))
        {
            DiagnosticsEvents.Source.Write(DiagnosticsEvents.Internal.Debug, new DiagnosticLog("NW.TcpListenerBase:Initialize", $"config-bind ep-v4-any={epV4Any}"));
        }

        _listener.Bind(epV4Any);
        _listener.Listen(_config.Backlog);

        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Debug))
        {
            DiagnosticsEvents.Source.Write(DiagnosticsEvents.Internal.Debug, new DiagnosticLog("NW.TcpListenerBase:Initialize", $"config-listen local-endpoint={_listener.LocalEndPoint}"));
        }
    }

    /// <summary>
    /// Applies per-connection socket options to an accepted client socket.
    /// </summary>
    /// <param name="socket">
    /// The accepted client socket to configure. Must not be <see langword="null"/>.
    /// </param>
    /// <remarks>
    /// <para>
    /// Called by <see cref="InitializeConnection(Socket, PooledAcceptContext)"/> immediately after a socket is accepted,
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
        socket.NoDelay = _config.NoDelay;
        socket.SendBufferSize = _config.BufferSize;
        socket.ReceiveBufferSize = _config.BufferSize;

        if (_config.KeepAlive)
        {
            // Enable TCP Keep-Alive -> OS will automatically send probes when connection idle.
            // WHY requires Keep-Alive: NAT/firewall usually drops the "silent" connection after a few minutes.
            // Keep-Alive keeps the connection alive and detects that the peer is dead (network failure).
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

            try
            {
                // Cross-platform API (.NET 5+): Windows, Linux, and macOS all support it.
                // Time = 120s: After 120 seconds of idle, start sending the first probe.
                socket.SetSocketOption(SocketOptionLevel.Tcp,
                                       SocketOptionName.TcpKeepAliveTime, 120);

                // Interval = 30s: If no response is given, send the next probe after 30 second.
                socket.SetSocketOption(SocketOptionLevel.Tcp,
                                       SocketOptionName.TcpKeepAliveInterval, 30);

                // RetryCount = 5: after 30 probes, there is no response -> connection dead -> close socket.
                // Total time to detect dead connection: 120 + (5 × 30) = 270 seconds.
                socket.SetSocketOption(SocketOptionLevel.Tcp,
                                       SocketOptionName.TcpKeepAliveRetryCount, 5);
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                // Fallback Windows-only: SIO_KEEPALIVE_VALS IOControl.
                // WHY fallback: Older runtime or restricted environment does not support cross-platform API.
                // SIO_KEEPALIVE_VALS = 12-byte struct: [on(4 bytes)][time_ms(4 bytes)][interval_ms(4 bytes)].
                if (OperatingSystem.IsWindows())
                {
                    const int on = 1;
                    const int time = 120_000;
                    const int interval = 30_000;

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

        // SO_REUSEPORT - multi-thread/process load balancing on Linux
        if (_config.ReusePort)
        {
            try
            {
                if (OperatingSystem.IsLinux())
                {
                    socket.SetSocketOption(SocketOptionLevel.Socket, ReusePortOption, 1);
                }
                else if (OperatingSystem.IsWindows())
                {
                    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseUnicastPort, 1);
                }
            }
            catch (SocketException ex) when (ex.SocketErrorCode is SocketError.OperationNotSupported or SocketError.ProtocolNotSupported)
            {
                // Graceful fallback
                if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Debug))
                {
                    DiagnosticsEvents.Source.Write(DiagnosticsEvents.Internal.Debug, new DiagnosticLog("NW.TcpListenerBase:InitializeOptions", "SO_REUSEPORT not-supported platform/kernel"));
                }
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex)) { /* Ignore if not supported. */ }
        }

        // TCP Fast Open (TFO) - reduces latency by 1 RTT
        if (_config.TcpFastOpen)
        {
            try
            {
                socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.FastOpen, 5);
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex)) { /* Ignore if not supported. */ }
        }

        if (OperatingSystem.IsLinux())
        {
            try
            {
                socket.SetSocketOption(SocketOptionLevel.Tcp, (SocketOptionName)3, 1);    // TCP_CORK = 3
                socket.SetSocketOption(SocketOptionLevel.Tcp, (SocketOptionName)0x0C, 1); // TCP_QUICKACK = 12
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex)) { /* Ignore if not supported. */ }
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
    private static bool IsIgnorableAcceptError(SocketError code, CancellationToken token)
        => token.IsCancellationRequested || code
        is SocketError.Shutdown
        or SocketError.TimedOut
        or SocketError.NotSocket
        or SocketError.WouldBlock
        or SocketError.Interrupted
        or SocketError.InvalidArgument
        or SocketError.OperationAborted;
}
