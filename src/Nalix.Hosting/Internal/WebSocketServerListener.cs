// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions.Networking;
using Nalix.Network.Listeners.Web;

namespace Nalix.Hosting.Internal;

/// <inheritdoc />
internal sealed class WebSocketServerListener : WebSocketListenerBase
{
    /// <inheritdoc />
    public WebSocketServerListener(IProtocol protocol, IConnectionHub hub, IConnectionGuard guard) : base(protocol, hub, guard) { }

    /// <inheritdoc />
    public WebSocketServerListener(ushort port, string path, IProtocol protocol, IConnectionHub hub, IConnectionGuard guard) : base(port, path, protocol, hub, guard) { }
}
