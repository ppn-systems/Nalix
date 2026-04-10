// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Networking;
using Nalix.Network.Listeners.Tcp;

namespace Nalix.Network.Hosting.Internal;

/// <inheritdoc />
internal sealed class TcpServerListener : TcpListenerBase
{
    /// <inheritdoc />
    public TcpServerListener(IProtocol protocol) : base(protocol) { }

    /// <inheritdoc />
    public TcpServerListener(ushort port, IProtocol protocol) : base(port, protocol) { }
}
