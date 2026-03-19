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
[System.Diagnostics.DebuggerDisplay("Uptime={Uptime}ms, Dropped={DroppedPackets}, Incoming={Incoming.Count}")]
internal sealed class FramedSocketCache
{
    #region Fields

    private readonly System.Int64 _startTime = (System.Int64)Clock.UnixTime().TotalMilliseconds;

    private System.Int64 _lastPingTime;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the connection uptime in milliseconds (how long the connection has been active).
    /// </summary>
    public System.Int64 Uptime => (System.Int64)Clock.UnixTime().TotalMilliseconds - this._startTime;

    /// <summary>
    /// Gets or sets the timestamp (in milliseconds) of the last received ping.
    /// Thread-safe via Interlocked operations.
    /// </summary>
    public System.Int64 LastPingTime
    {
        get => System.Threading.Interlocked.Read(ref _lastPingTime);
        set => System.Threading.Interlocked.Exchange(ref _lastPingTime, value);
    }

    #endregion Properties
}
