// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Abstractions.Concurrency;
using Nalix.Abstractions.Networking;
using Nalix.Network.Listeners.Udp;

namespace Nalix.Hosting.Internal;

/// <inheritdoc />
internal sealed class UdpServerListener : UdpListenerBase
{
    private readonly Func<IConnection, System.Net.EndPoint, ReadOnlySpan<byte>, bool>? _authen;

    /// <inheritdoc />
    public UdpServerListener(IProtocol protocol, IConnectionHub hub, ITaskManager taskManager)
        : base(protocol, hub, taskManager) { }

    /// <inheritdoc />
    public UdpServerListener(ushort port, IProtocol protocol, IConnectionHub hub, ITaskManager taskManager)
        : base(port, protocol, hub, taskManager) { }

    /// <inheritdoc />
    public UdpServerListener(ushort port, IProtocol protocol, IConnectionHub hub, ITaskManager taskManager, Func<IConnection, System.Net.EndPoint, ReadOnlySpan<byte>, bool> authen)
        : base(port, protocol, hub, taskManager) => _authen = authen;

    /// <inheritdoc />
    public UdpServerListener(IProtocol protocol, IConnectionHub hub, ITaskManager taskManager, Func<IConnection, System.Net.EndPoint, ReadOnlySpan<byte>, bool> authen)
        : base(protocol, hub, taskManager) => _authen = authen;

    /// <inheritdoc />
    public override bool IsAuthenticated(IConnection connection, System.Net.EndPoint remoteEndPoint, ReadOnlySpan<byte> payload)
    {
        if (_authen != null)
        {
            return _authen(connection, remoteEndPoint, payload);
        }

        // By default, hosting allows all datagrams that pass the session token check.
        return true;
    }
}
