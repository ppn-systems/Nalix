// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.ComponentModel;
using Nalix.Common.Networking;
using Nalix.Common.Shared;
using Nalix.Network.Listeners.Tcp;

namespace Nalix.Network.Internal.Pooled;

/// <summary>
/// Represents a pooled context used during TCP listener processing.
/// </summary>
/// <remarks>
/// This object is reused via a pooling mechanism to reduce allocations
/// when handling incoming TCP connections.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
internal sealed class PooledListenerProcessContext : IPoolable
{
    /// <summary>
    /// Gets or sets the active connection associated with the current processing context.
    /// </summary>
    public IConnection Connection;

    /// <summary>
    /// Gets or sets the TCP listener responsible for handling the connection.
    /// </summary>
    public TcpListenerBase Listener;

    /// <summary>
    /// Resets the state of the object before returning it to the pool.
    /// </summary>
    /// <remarks>
    /// Clears all references to avoid memory leaks and ensure a clean state
    /// for the next usage.
    /// </remarks>
    public void ResetForPool()
    {
        Listener = null;
        Connection = null;
    }
}
