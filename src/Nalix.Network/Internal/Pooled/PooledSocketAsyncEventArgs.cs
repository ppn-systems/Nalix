// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.ComponentModel;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using Nalix.Common.Abstractions;

#if DEBUG
[assembly: InternalsVisibleTo("Nalix.Network.Tests")]
[assembly: InternalsVisibleTo("Nalix.Network.Benchmarks")]
#endif

namespace Nalix.Network.Internal.Pooled;

/// <summary>
/// A pooled wrapper around <see cref="SocketAsyncEventArgs"/> that resets state before returning to the pool.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
[DebuggerDisplay("HasSocket={AcceptSocket != null}, HasContext={Context != null}")]
internal sealed class PooledSocketAsyncEventArgs : SocketAsyncEventArgs, IPoolable
{
    /// <summary>
    /// The pooled accept context associated with this event args.
    /// </summary>
    public PooledAcceptContext? Context { get; set; }

    /// <summary>
    /// Resets the internal state before returning to the pool.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ResetForPool()
    {
        // Unsubscribe event handlers if needed here (e.g. this.Completed -= SomeHandler)
        this.Context = null;
        this.UserToken = null;
        this.AcceptSocket = null;
        this.RemoteEndPoint = null;
        this.SetBuffer(null, 0, 0);
    }
}
