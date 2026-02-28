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
    /// <inheritdoc />
    public UdpServerListener(IProtocol protocol) : base(protocol) { }

    /// <inheritdoc />
    public UdpServerListener(ushort port, IProtocol protocol) : base(port, protocol) { }

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0022:Use expression body for method", Justification = "<Pending>")]
    protected override bool IsAuthenticated(IConnection connection, EndPoint remoteEndPoint, ReadOnlySpan<byte> payload)
    {
        // By default, hosting allows all datagrams that pass the session token check.
        // Custom authentication logic can be added by providing a specialized listener.
        return true;
    }
}
