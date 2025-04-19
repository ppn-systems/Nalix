using Notio.Network.Configurations;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using System.Runtime.CompilerServices;

namespace Notio.Network.Listeners;

public abstract partial class Listener
{
    /// <summary>
    /// Creates the byte array for configuring Keep-Alive on Windows.
    /// </summary>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte[] KeepAliveConfig()
    {
        int on = 1;              // Turning on Keep-Alive
        int time = 10_000;       // 10 seconds without data, send Keep-Alive
        int interval = 5_000;    // SendPacket every 5 seconds if there is no response

        byte[] keepAlive = new byte[12];
        BitConverter.GetBytes(on).CopyTo(keepAlive, 0);
        BitConverter.GetBytes(time).CopyTo(keepAlive, 4);
        BitConverter.GetBytes(interval).CopyTo(keepAlive, 8);

        return keepAlive;
    }

    /// <summary>
    /// Configures the socket for high-performance operation by setting buffer sizes, timeouts, and keep-alive options.
    /// </summary>
    /// <param name="socket">The socket to configure.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
    [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]
    private static void ConfigureHighPerformanceSocket(Socket socket)
    {
        // Performance tuning
        socket.NoDelay = Config.NoDelay;
        socket.SendBufferSize = Config.BufferSize;
        socket.ReceiveBufferSize = Config.BufferSize;
        socket.LingerState = new LingerOption(true, TcpConfig.False);

        if (Config.KeepAlive)
        {
            // Windows specific settings
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

            if (Config.IsWindows)
            {
                // Windows specific settings
                socket.IOControl(IOControlCode.KeepAliveValues, KeepAliveConfig(), null);
            }
        }

        if (!Config.IsWindows)
        {
            // Linux, MacOS, etc.
            socket.SetSocketOption(
                SocketOptionLevel.Socket,
                SocketOptionName.ReuseAddress, Config.ReuseAddress ?
                TcpConfig.True : TcpConfig.False);
        }
    }
}
