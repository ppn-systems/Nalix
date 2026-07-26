// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
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
    private void ConfigureListenerSocket(Socket listener)
    {
        listener.Blocking = true;
        listener.ExclusiveAddressUse = OperatingSystem.IsWindows() || !_config.ReuseAddress;
        listener.LingerState = new LingerOption(false, 0);

        // ReuseAddress MUST be set BEFORE Bind.
        // WHY: Allows binding the port again immediately after the server restart (avoid "Address already in use").
        listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, _config.ReuseAddress && !OperatingSystem.IsWindows() ? 1 : 0);

        // Buffer sizes: set on listener socket so that they are inherited by accepted sockets on Windows and Linux.
        // WHY: Setting Send/Receive buffer sizes on the listening socket propagates the defaults to all accepted sockets,
        // avoiding individual SetSocketOption calls per connection.
        listener.SendBufferSize = _config.BufferSize;
        listener.ReceiveBufferSize = _config.BufferSize;

        if (_config.KeepAlive)
        {
            // Enable TCP Keep-Alive -> OS will automatically send probes when connection idle.
            // Configured on the listener socket so it is automatically inherited by accepted client sockets.
            listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

            try
            {
                // Cross-platform API (.NET 5+): Windows, Linux, and macOS all support it.
                listener.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 120);
                listener.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 30);
                listener.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, 5);
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                // Fallback Windows-only: SIO_KEEPALIVE_VALS IOControl.
                if (OperatingSystem.IsWindows())
                {
                    const int on = 1;
                    const int time = 120_000;
                    const int interval = 30_000;

                    byte[] vals = new byte[12];
                    System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(MemoryExtensions.AsSpan(vals)[0..4], on);
                    System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(MemoryExtensions.AsSpan(vals)[4..8], time);
                    System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(MemoryExtensions.AsSpan(vals)[8..12], interval);
                    _ = listener.IOControl(IOControlCode.KeepAliveValues, vals, null);
                }
            }
        }

        // SO_REUSEPORT - multi-thread/process load balancing.
        // Must be configured on the listener socket before Bind.
        if (_config.ReusePort)
        {
            try
            {
                if (OperatingSystem.IsLinux())
                {
                    listener.SetSocketOption(SocketOptionLevel.Socket, ReusePortOption, 1);
                }
            }
            catch (SocketException ex) when (ex.SocketErrorCode is SocketError.OperationNotSupported or SocketError.ProtocolNotSupported)
            {
                if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Debug))
                {
                    DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Debug,
                        new DiagnosticLog("NW.TcpListenerBase:ConfigureListenerSocket", "SO_REUSEPORT not-supported platform/kernel"));
                }
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex)) { /* Ignore if not supported. */ }
        }

        // TCP Fast Open (TFO) - reduces latency by 1 RTT.
        // Configured on the listener socket before Listen.
        if (_config.TcpFastOpen)
        {
            try
            {
                listener.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.FastOpen, 5);
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex)) { /* Ignore if not supported. */ }
        }
    }

    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Initialize()
    {
        if (_config.EnableDualStack)
        {
            // Try creating an IPv6 socket with DualMode first.
            // DualMode = true -> 1 socket that receives both IPv6 and IPv4-mapped (::ffff:x.x.x.x).
            // WHY prioritizes IPv6: Future-proof, supporting IPv4 clients via dual-stack.
            Socket? sock = null;

            try
            {
                sock = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp)
                {
                    DualMode = _config.DualMode
                };
                this.ConfigureListenerSocket(sock);

                // IPv6Any (::) -> listens on all IPv6 interfaces (and IPv4 via DualMode).
                IPEndPoint epV6Any = new(IPAddress.IPv6Any, _port);

                if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Debug))
                {
                    DiagnosticsEvents.Write(
                        DiagnosticsEvents.Internal.Debug,
                        new DiagnosticLog("NW.TcpListenerBase:Initialize",
                            $"config-bind mode={(sock.DualMode ? "dual-stack" : "ipv6-only")} " +
                            $"address-family={sock.AddressFamily} dual-mode={sock.DualMode} ep-v6-any={epV6Any}"));
                }

                sock.Bind(epV6Any);
                sock.Listen(_config.Backlog);

                _listener = sock;

                if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Information))
                {
                    DiagnosticsEvents.Write(
                        DiagnosticsEvents.Internal.Information,
                        new DiagnosticLog("NW.TcpListenerBase:Initialize",
                            $"config-listen mode={(sock.DualMode ? "dual-stack" : "ipv6-only")} local-endpoint={_listener.LocalEndPoint}"));
                }

                return;
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                // IPv6/DualMode is not supported on this environment -> IPv4 fallback.
                // WHY not rethrow: Failover automatically is better than crashing the server.
                if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Warning))
                {
                    DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Warning,
                        new DiagnosticLog("NW.TcpListenerBase:Initialize", "failed-bind", ex));
                }

                try
                {
                    sock?.Close();
                }
                catch (ObjectDisposedException closeEx)
                {
                    if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Debug))
                    {
                        DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Debug,
                            new DiagnosticLog("NW.TcpListenerBase:Initialize", $"ipv6-fallback-close-ignored type={closeEx.GetType().Name}", ex));
                    }
                }
                catch (Exception closeEx) when (ExceptionClassifier.IsNonFatal(closeEx))
                {
                    if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Warning))
                    {
                        DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Warning,
                            new DiagnosticLog("NW.TcpListenerBase:Initialize", "ipv6-fallback-close-failed", closeEx));
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
                        DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Warning,
                            new DiagnosticLog("NW.TcpListenerBase:Initialize", "ipv6-fallback-dispose-failed", disposeEx));
                    }
                }
            }
        }

        // Fallback: IPv4-only socket.
        // Used when: EnableIPv6 = false, or IPv6 bind fails.
        _listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        this.ConfigureListenerSocket(_listener);

        IPEndPoint epV4Any = new(IPAddress.Any, _port);

        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Debug))
        {
            DiagnosticsEvents.Write(
                DiagnosticsEvents.Internal.Debug,
                new DiagnosticLog("NW.TcpListenerBase:Initialize", $"config-bind mode=ipv4-only ep-v4-any={epV4Any}"));
        }

        _listener.Bind(epV4Any);
        _listener.Listen(_config.Backlog);

        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Debug))
        {
            DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Debug,
                new DiagnosticLog("NW.TcpListenerBase:Initialize", $"config-listen local-endpoint={_listener.LocalEndPoint}"));
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
    /// </list>
    /// </para>
    /// <para>
    /// Note on first-chance SocketExceptions and Connection Churn:
    /// Under high connection churn or DDoS conditions, client sockets may abort or close immediately
    /// after being accepted but before options are applied. Calling Socket.SetSocketOption on such
    /// closed sockets triggers first-chance SocketException events.
    /// </para>
    /// <para>
    /// To minimize these first-chance exception events, we keep only the absolute essential options
    /// (Blocking and NoDelay) on accepted sockets, and move all inheritable options (SendBufferSize,
    /// ReceiveBufferSize, KeepAlive settings, ReusePort, and FastOpen) to the listener socket.
    /// The OS automatically propagates listener-configured settings to accepted client sockets,
    /// avoiding the need to invoke setsockopt for these options on every new connection.
    /// </para>
    /// </remarks>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining)]
    [SuppressMessage(
        "CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
    [SuppressMessage(
        "Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]
    protected bool InitializeOptions(Socket socket)
    {
        ArgumentNullException.ThrowIfNull(socket, nameof(socket));

        try
        {
            // Keep the socket in blocking mode.
            // WHY: Task-based async I/O works well with socket blocking.
            // Non-blocking mode requires handling WouldBlock errors in every recv/send call.
            socket.Blocking = true;

            // OS-level Nagle algorithm control.
            socket.NoDelay = _config.NoDelay;

            return true;
        }
        catch (SocketException ex) when (ex.SocketErrorCode is
            SocketError.ConnectionAborted or
            SocketError.ConnectionReset or
            SocketError.NotSocket or
            SocketError.OperationAborted or
            SocketError.Shutdown or
            SocketError.Disconnecting or
            SocketError.Fault)
        {
            return false;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }
}
