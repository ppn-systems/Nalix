// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Common.Networking;
using Nalix.Network.Listeners.Udp;

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Benchmarks")]
#endif

namespace Nalix.Hosting.Internal;

/// <inheritdoc />
internal sealed class UdpServerListener : UdpListenerBase
{
    private readonly Func<IConnection, System.Net.EndPoint, ReadOnlySpan<byte>, bool>? _authen;

    /// <inheritdoc />
    public UdpServerListener(IProtocol protocol, IConnectionHub hub) : base(protocol, hub) { }

    /// <inheritdoc />
    public UdpServerListener(ushort port, IProtocol protocol, IConnectionHub hub) : base(port, protocol, hub) { }

    /// <inheritdoc />
    public UdpServerListener(ushort port, IProtocol protocol, IConnectionHub hub, Func<IConnection, System.Net.EndPoint, ReadOnlySpan<byte>, bool> authen)
        : base(port, protocol, hub) => _authen = authen;

    /// <inheritdoc />
    public UdpServerListener(IProtocol protocol, IConnectionHub hub, Func<IConnection, System.Net.EndPoint, ReadOnlySpan<byte>, bool> authen)
        : base(protocol, hub) => _authen = authen;

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
