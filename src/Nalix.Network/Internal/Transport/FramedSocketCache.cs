// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Framework.Time;

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Benchmarks")]
#endif

namespace Nalix.Network.Internal.Transport;

/// <summary>
/// Provides a thread-safe caching layer for network packets, supporting both outgoing and incoming traffic.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
[System.Diagnostics.DebuggerDisplay("Uptime={Uptime}ms, Dropped={DroppedPackets}, Incoming={Incoming.Count}")]
internal sealed class FramedSocketCache
{

    #region Properties

    /// <summary>
    /// Gets the connection uptime in milliseconds (how long the connection has been active).
    /// </summary>
    public long Uptime { get => (long)Clock.UnixTime().TotalMilliseconds - field; } = (long)Clock.UnixTime().TotalMilliseconds;

    /// <summary>
    /// Gets or sets the timestamp (in milliseconds) of the last received ping.
    /// Thread-safe via Interlocked operations.
    /// </summary>
    public long LastPingTime
    {
        get => System.Threading.Interlocked.Read(ref field);
        set => System.Threading.Interlocked.Exchange(ref field, value);
    }

    #endregion Properties
}
