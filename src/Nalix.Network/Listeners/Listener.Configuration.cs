namespace Nalix.Network.Listeners;

public abstract partial class Listener
{
    /// <summary>
    /// Configures the socket for high-performance operation by setting buffer sizes, timeouts, and keep-alive options.
    /// </summary>
    /// <param name="socket">The socket to configure.</param>
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
        // socket.LingerState = new LingerOption(true, SocketSettings.False);

        // if using async or non-blocking I/O.
        socket.Blocking = false;

        if (Config.KeepAlive)
        {
            // Windows specific settings
            socket.SetSocketOption(System.Net.Sockets.SocketOptionLevel.Socket,
                                   System.Net.Sockets.SocketOptionName.KeepAlive, true);

            if (Config.IsWindows)
            {
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
        }
    }
}
