// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions.Concurrency;
using Nalix.Abstractions.Networking;
using Nalix.Network.Listeners.Tcp;

namespace Nalix.Hosting.Internal;

/// <inheritdoc />
internal sealed class TcpServerListener : TcpListenerBase
{
    /// <inheritdoc />
    public TcpServerListener(IProtocol protocol, IConnectionHub hub, IConnectionGuard guard, ITaskManager taskManager) : base(protocol, hub, guard, taskManager) { }

    /// <inheritdoc />
    public TcpServerListener(ushort port, IProtocol protocol, IConnectionHub hub, IConnectionGuard guard, ITaskManager taskManager) : base(port, protocol, hub, guard, taskManager) { }
}
