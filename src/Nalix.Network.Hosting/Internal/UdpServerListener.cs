// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Net;
using Nalix.Common.Networking;
using Nalix.Network.Listeners.Udp;

namespace Nalix.Network.Hosting.Internal;

/// <inheritdoc />
internal sealed class UdpServerListener : UdpListenerBase
{
    private readonly Func<IConnection, System.Net.EndPoint, ReadOnlySpan<byte>, bool>? _authen;

    /// <inheritdoc />
    public UdpServerListener(IProtocol protocol) : base(protocol) { }

    /// <inheritdoc />
    public UdpServerListener(ushort port, IProtocol protocol) : base(port, protocol) { }

    /// <inheritdoc />
    public UdpServerListener(ushort port, IProtocol protocol, Func<IConnection, System.Net.EndPoint, ReadOnlySpan<byte>, bool> authen)
        : base(port, protocol)
    {
        _authen = authen;
    }

    /// <inheritdoc />
    public UdpServerListener(IProtocol protocol, Func<IConnection, System.Net.EndPoint, ReadOnlySpan<byte>, bool> authen)
        : base(protocol)
    {
        _authen = authen;
    }

    /// <inheritdoc />
    protected override bool IsAuthenticated(IConnection connection, System.Net.EndPoint remoteEndPoint, ReadOnlySpan<byte> payload)
    {
        if (_authen != null)
        {
            return _authen(connection, remoteEndPoint, payload);
        }

        // By default, hosting allows all datagrams that pass the session token check.
        return true;
    }
}
