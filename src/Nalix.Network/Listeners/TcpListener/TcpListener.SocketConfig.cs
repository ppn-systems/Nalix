// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Logging.Abstractions;
using Nalix.Framework.Injection;

namespace Nalix.Network.Listeners.Tcp;

public abstract partial class TcpListenerBase
{
    /// <summary>
    /// Configures the socket for high-performance operation by setting buffer sizes, timeouts, and keep-alive options.
    /// </summary>
    /// <param name="socket">The socket to configure.</param>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]
    private static void ConfigureHighPerformanceSocket(System.Net.Sockets.Socket socket)
    {
        // Performance tuning
        socket.NoDelay = Config.NoDelay;
        socket.SendBufferSize = Config.BufferSize;
        socket.ReceiveBufferSize = Config.BufferSize;

        // When you want to disconnect immediately without making sure the data has been sent.
        // socket.LingerState = new LingerOption(true, NetworkSocketOptions.False);

        // Keep the accepted socket in blocking mode; Task-based async works fine with blocking sockets.
        // If you really want non-blocking I/O, ensure your Accept/Receive loops expect WouldBlock.
        socket.Blocking = true;

        if (Config.KeepAlive)
        {
            // Windows specific settings
            socket.SetSocketOption(System.Net.Sockets.SocketOptionLevel.Socket,
                                   System.Net.Sockets.SocketOptionName.KeepAlive, true);

            if (Config.IsWindows)
            {
                // Win32 SIO_KEEPALIVE_VALS: [on(4)][time(4 ms)][interval(4 ms)]
                // 1. Turning on Keep-Alive
                // 2. 3 seconds without data, send Keep-Alive
                // 3. Send every 1 second if there is no response

                const System.Int32 on = 1;
                const System.Int32 time = 3_000;
                const System.Int32 interval = 1_000;

                System.Span<System.Byte> keepAlive = stackalloc System.Byte[12];

                System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(keepAlive[..4], on);
                System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(keepAlive.Slice(4, 4), time);
                System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(keepAlive.Slice(8, 4), interval);

                // Windows specific settings
                _ = socket.IOControl(System.Net.Sockets.IOControlCode.KeepAliveValues, keepAlive.ToArray(), null);
            }
            else if (!Config.IsWindows)
            {
                try
                {
                    // These may be supported on modern .NET / kernels:
                    // TcpKeepAliveTime (seconds), value example: 3
                    // TcpKeepAliveInterval (seconds), value example: 1
                    // TcpKeepAliveRetryCount, value example: 5

                    socket.SetSocketOption(System.Net.Sockets.SocketOptionLevel.Tcp, System.Net.Sockets.SocketOptionName.DontRoute, 3);
                    socket.SetSocketOption(System.Net.Sockets.SocketOptionLevel.Tcp, System.Net.Sockets.SocketOptionName.UnblockSource, 1);
                    socket.SetSocketOption(System.Net.Sockets.SocketOptionLevel.Tcp, System.Net.Sockets.SocketOptionName.TcpKeepAliveInterval, 5);
                }
                catch { /* best-effort, ignore if not supported */ }
            }
        }
    }

    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void Initialize()
    {
        if (Config.EnableIPv6)
        {
            // Try IPv6 + DualMode first
            System.Net.Sockets.Socket? listener = null;
            try
            {
                listener = new System.Net.Sockets.Socket(
                    System.Net.Sockets.AddressFamily.InterNetworkV6,
                    System.Net.Sockets.SocketType.Stream,
                    System.Net.Sockets.ProtocolType.Tcp)
                {
                    ExclusiveAddressUse = !Config.ReuseAddress,                  // fast rebind combo with ReuseAddress
                    LingerState = new System.Net.Sockets.LingerOption(false, 0),
                    Blocking = true,
                    // Must set before Bind
                    DualMode = true
                };

                // Reuse BEFORE bind
                listener.SetSocketOption(
                    System.Net.Sockets.SocketOptionLevel.Socket,
                    System.Net.Sockets.SocketOptionName.ReuseAddress,
                    Config.ReuseAddress ? 1 : 0);

                // Optional: larger listen buffer (per-connection tuning is more important)
                listener.SetSocketOption(
                    System.Net.Sockets.SocketOptionLevel.Socket,
                    System.Net.Sockets.SocketOptionName.ReceiveBuffer,
                    Config.BufferSize);

                var epV6Any = new System.Net.IPEndPoint(System.Net.IPAddress.IPv6Any, this._port);

                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Debug($"[{nameof(TcpListenerBase)}] config-bind {epV6Any} (v6)");

                listener.Bind(epV6Any);
                listener.Listen(Config.Backlog);

                this._listener = listener;
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Debug($"[{nameof(TcpListenerBase)}] config-listen {this._listener.LocalEndPoint} (dual)");
                return;
            }
            catch
            {
                // Clean up the half-initialized IPv6 socket before falling back
                try { listener?.Close(); } catch { }
                try { listener?.Dispose(); } catch { }
                listener = null;
            }
        }

        // Fallback: IPv4-only
        this._listener = new System.Net.Sockets.Socket(
            System.Net.Sockets.AddressFamily.InterNetwork,
            System.Net.Sockets.SocketType.Stream,
            System.Net.Sockets.ProtocolType.Tcp)
        {
            ExclusiveAddressUse = !Config.ReuseAddress,
            LingerState = new System.Net.Sockets.LingerOption(false, 0),
            Blocking = true
        };

        this._listener.SetSocketOption(
            System.Net.Sockets.SocketOptionLevel.Socket,
            System.Net.Sockets.SocketOptionName.ReuseAddress,
            Config.ReuseAddress ? 1 : 0);

        this._listener.SetSocketOption(
            System.Net.Sockets.SocketOptionLevel.Socket,
            System.Net.Sockets.SocketOptionName.ReceiveBuffer,
            Config.BufferSize);

        var epV4Any = new System.Net.IPEndPoint(System.Net.IPAddress.Any, this._port);

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[{nameof(TcpListenerBase)}] config-bind {epV4Any} (v4)");

        _listener.Bind(epV4Any);
        _listener.Listen(Config.Backlog);

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[{nameof(TcpListenerBase)}] config-listen {this._listener.LocalEndPoint}");
    }

    /// <summary>
    /// Classifies socket errors that are expected/ignorable during shutdown.
    /// </summary>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Boolean IsIgnorableAcceptError(System.Net.Sockets.SocketError code, System.Threading.CancellationToken token)
        => token.IsCancellationRequested || code is System.Net.Sockets.SocketError.OperationAborted
        or System.Net.Sockets.SocketError.Interrupted or System.Net.Sockets.SocketError.NotSocket
        or System.Net.Sockets.SocketError.InvalidArgument or System.Net.Sockets.SocketError.TimedOut
        or System.Net.Sockets.SocketError.WouldBlock or System.Net.Sockets.SocketError.Shutdown;
}