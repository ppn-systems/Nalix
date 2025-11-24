// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Networking.Abstractions;
using Nalix.Common.Shared.Caching;
using Nalix.Network.Listeners.Tcp;

namespace Nalix.Network.Internal.Pooled;

internal sealed class PooledListenerProcessContext : IPoolable
{
    public IConnection Connection;
    public TcpListenerBase Listener;

    public void ResetForPool()
    {
        Listener = null;
        Connection = null;
    }
}
