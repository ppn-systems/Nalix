using System;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using System.Runtime.CompilerServices;

namespace Notio.Network.Listeners;

public abstract partial class Listener
{
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

        // When you want to disconnect immediately without making sure the data has been sent.
        // socket.LingerState = new LingerOption(true, TcpConfig.False);

        // if using async or non-blocking I/O.
        socket.Blocking = false;

        if (Config.KeepAlive)
        {
            // Windows specific settings
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

            if (Config.IsWindows)
            {
                // 1. Turning on Keep-Alive
                // 2. 3 seconds without data, send Keep-Alive 
                // 3. Send every 1 second if there is no response

                const int on = 1;
                const int time = 3_000;
                const int interval = 1_000;

                Span<byte> keepAlive = stackalloc byte[12];

                BinaryPrimitives.WriteInt32LittleEndian(keepAlive[..4], on);
                BinaryPrimitives.WriteInt32LittleEndian(keepAlive.Slice(4, 4), time);
                BinaryPrimitives.WriteInt32LittleEndian(keepAlive.Slice(8, 4), interval);

                // Windows specific settings
                socket.IOControl(IOControlCode.KeepAliveValues, keepAlive.ToArray(), null);
            }
        }
    }
}
