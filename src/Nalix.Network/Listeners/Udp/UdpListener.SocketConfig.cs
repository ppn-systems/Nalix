namespace Nalix.Network.Listeners.Udp;

public abstract partial class UdpListenerBase
{
    private void InitializeUdpClient()
    {
        _udpClient = new System.Net.Sockets.UdpClient(Config.Port)
        {
            Client = { ExclusiveAddressUse = !Config.ReuseAddress }
        };

        ConfigureHighPerformanceSocket(_udpClient.Client);
        this._logger.Debug("[UDP] UDP client bound to port {0}", Config.Port);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]
    private static void ConfigureHighPerformanceSocket(System.Net.Sockets.Socket socket)
    {
        socket.NoDelay = Config.NoDelay;
        socket.SendBufferSize = Config.BufferSize;
        socket.ReceiveBufferSize = Config.BufferSize;

        socket.Blocking = false;

        if (Config.KeepAlive)
        {
            socket.SetSocketOption(System.Net.Sockets.SocketOptionLevel.Socket,
                                   System.Net.Sockets.SocketOptionName.KeepAlive, true);

            if (Config.IsWindows)
            {
                const System.Int32 on = 1;
                const System.Int32 time = 3_000;
                const System.Int32 interval = 1_000;

                System.Span<System.Byte> keepAlive = stackalloc System.Byte[12];
                System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(keepAlive[..4], on);
                System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(keepAlive.Slice(4, 4), time);
                System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(keepAlive.Slice(8, 4), interval);

                _ = socket.IOControl(System.Net.Sockets.IOControlCode.KeepAliveValues, keepAlive.ToArray(), null);
            }
        }
    }
}