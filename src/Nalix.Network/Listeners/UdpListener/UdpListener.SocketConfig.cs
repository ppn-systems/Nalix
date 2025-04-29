// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Logging;
using Nalix.Framework.Injection;

namespace Nalix.Network.Listeners.Udp;

public abstract partial class UdpListenerBase
{
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void Initialize()
    {
        _udpClient = new System.Net.Sockets.UdpClient(_port)
        {
            Client = { ExclusiveAddressUse = !Config.ReuseAddress }
        };

        ConfigureHighPerformanceSocket(_udpClient.Client);

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[{nameof(UdpListenerBase)}] init-ok port={_port} " +
                                       $"reuse={Config.ReuseAddress} buf={Config.BufferSize}");
    }

    [System.Diagnostics.DebuggerStepThrough]
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