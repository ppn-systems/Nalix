// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Logging;
using Nalix.Framework.Injection;

namespace Nalix.Network.Listeners.Tcp;

public abstract partial class TcpListenerBase
{
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
    System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void Initialize()
    {
        if (Config.EnableIPv6)
        {
            // Try IPv6 + DualMode first
            System.Net.Sockets.Socket listener = null;
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
                                        .Debug($"[{nameof(TcpListenerBase)}:{nameof(Initialize)}] config-bind {epV6Any} (v6)");

                listener.Bind(epV6Any);
                listener.Listen(Config.Backlog);

                _listener = listener;
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Debug($"[{nameof(TcpListenerBase)}:{nameof(Initialize)}] " +
                                               $"config-listen {_listener.LocalEndPoint} (dual)");
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
        _listener = new System.Net.Sockets.Socket(
            System.Net.Sockets.AddressFamily.InterNetwork,
            System.Net.Sockets.SocketType.Stream,
            System.Net.Sockets.ProtocolType.Tcp)
        {
            ExclusiveAddressUse = !Config.ReuseAddress,
            LingerState = new System.Net.Sockets.LingerOption(false, 0),
            Blocking = true
        };

        _listener.SetSocketOption(
            System.Net.Sockets.SocketOptionLevel.Socket,
            System.Net.Sockets.SocketOptionName.ReuseAddress,
            Config.ReuseAddress ? 1 : 0);

        _listener.SetSocketOption(
            System.Net.Sockets.SocketOptionLevel.Socket,
            System.Net.Sockets.SocketOptionName.ReceiveBuffer,
            Config.BufferSize);

        var epV4Any = new System.Net.IPEndPoint(System.Net.IPAddress.Any, this._port);

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[{nameof(TcpListenerBase)}:{nameof(Initialize)}] config-bind {epV4Any} (v4)");

        _listener.Bind(epV4Any);
        _listener.Listen(Config.Backlog);

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[{nameof(TcpListenerBase)}:{nameof(Initialize)}] config-listen {_listener.LocalEndPoint}");
    }

    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]
    private static void InitializeSocketOptions(System.Net.Sockets.Socket socket)
    {
        // Performance tuning
        socket.NoDelay = Config.NoDelay;
        socket.SendBufferSize = Config.BufferSize;
        socket.ReceiveBufferSize = Config.BufferSize;

        // When you want to disconnect immediately without making sure the data has been sent.
        // socket.LingerState = new LingerOption(true, NetworkSocketOptions.False);

        // Keep the accepted socket in blocking mode; Task-based async works fine with blocking sockets.
        // If you really want non-blocking IEndpointKey /O, ensure your Accept/Receive loops expect WouldBlock.
        socket.Blocking = true;

        if (Config.KeepAlive)
        {
            // Windows specific settings
            socket.SetSocketOption(System.Net.Sockets.SocketOptionLevel.Socket,
                                   System.Net.Sockets.SocketOptionName.KeepAlive, true);

            try
            {
                // Cross-platform in modern .NET
                socket.SetSocketOption(System.Net.Sockets.SocketOptionLevel.Tcp,
                                       System.Net.Sockets.SocketOptionName.TcpKeepAliveTime, 3);

                socket.SetSocketOption(System.Net.Sockets.SocketOptionLevel.Tcp,
                                       System.Net.Sockets.SocketOptionName.TcpKeepAliveInterval, 1);

                socket.SetSocketOption(System.Net.Sockets.SocketOptionLevel.Tcp,
                                       System.Net.Sockets.SocketOptionName.TcpKeepAliveRetryCount, 3);
            }
            catch
            {
                // Fallback Windows-only SIO_KEEPALIVE_VALS if needed
                if (System.OperatingSystem.IsWindows())
                {
                    // Win32 SIO_KEEPALIVE_VALS: [on(4)][time(4 ms)][interval(4 ms)]
                    // 1. Turning on Keep-Alive
                    // 2. 3 seconds without data, send Keep-Alive
                    // 3. Send every 1 second if there is no response

                    const System.Int32 on = 1;
                    const System.Int32 time = 3_000;
                    const System.Int32 interval = 1_000;

                    System.Byte[] vals = new System.Byte[12];
                    System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(System.MemoryExtensions.AsSpan(vals)[0..4], on);
                    System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(System.MemoryExtensions.AsSpan(vals)[4..8], time);
                    System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(System.MemoryExtensions.AsSpan(vals)[8..12], interval);
                    _ = socket.IOControl(System.Net.Sockets.IOControlCode.KeepAliveValues, vals, null);
                }
            }
        }
    }

    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Boolean IsIgnorableAcceptError(System.Net.Sockets.SocketError code, System.Threading.CancellationToken token)
        => token.IsCancellationRequested || code is System.Net.Sockets.SocketError.OperationAborted
        or System.Net.Sockets.SocketError.Interrupted or System.Net.Sockets.SocketError.NotSocket
        or System.Net.Sockets.SocketError.InvalidArgument or System.Net.Sockets.SocketError.TimedOut
        or System.Net.Sockets.SocketError.WouldBlock or System.Net.Sockets.SocketError.Shutdown;
}