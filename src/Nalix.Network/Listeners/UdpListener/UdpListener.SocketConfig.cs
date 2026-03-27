// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using Nalix.Common.Diagnostics;
using Nalix.Framework.Injection;

namespace Nalix.Network.Listeners.Udp;

public abstract partial class UdpListenerBase
{
    /// <summary>
    /// Initializes the underlying <see cref="UdpClient"/> and applies the listener socket configuration.
    /// </summary>
    /// <remarks>
    /// Derived types can override this method to customize how the UDP socket is created or bound,
    /// but should preserve the contract that <see cref="_udpClient"/> is ready for receive operations
    /// when the method returns.
    /// </remarks>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining)]
    protected virtual void Initialize()
    {
        _udpClient = new UdpClient(_port)
        {
            Client = { ExclusiveAddressUse = !Config.ReuseAddress }
        };

        this.ConfigureHighPerformanceSocket(_udpClient.Client);

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[NW.{nameof(UdpListenerBase)}:{nameof(Initialize)}] init-ok port={_port} reuse={Config.ReuseAddress} buf={Config.BufferSize}");
    }

    /// <summary>
    /// Applies the listener's socket-level performance tuning.
    /// </summary>
    /// <param name="socket">The socket to configure.</param>
    /// <remarks>
    /// Override this method when a derived listener needs a different tuning profile, such as
    /// platform-specific socket options or custom buffer sizing. Implementations should be careful
    /// to preserve the non-blocking and buffer configuration expectations of the base receive loop.
    /// </remarks>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining)]
    [SuppressMessage(
        "CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
    [SuppressMessage(
        "Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]
    protected virtual void ConfigureHighPerformanceSocket(Socket socket)
    {
        socket.Blocking = false;
        socket.NoDelay = Config.NoDelay;
        socket.SendBufferSize = Config.BufferSize;
        socket.ReceiveBufferSize = Config.BufferSize;

        if (Config.KeepAlive)
        {
            socket.SetSocketOption(SocketOptionLevel.Socket,
                                   SocketOptionName.KeepAlive, true);

            if (OperatingSystem.IsWindows())
            {
                const int on = 1;
                const int time = 3_000;
                const int interval = 1_000;

                Span<byte> keepAlive = stackalloc byte[12];
                System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(keepAlive[..4], on);
                System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(keepAlive.Slice(4, 4), time);
                System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(keepAlive.Slice(8, 4), interval);

                _ = socket.IOControl(IOControlCode.KeepAliveValues, keepAlive.ToArray(), null);
            }
        }
    }
}
