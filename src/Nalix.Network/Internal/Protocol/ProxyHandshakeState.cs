// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Net.Sockets;
using Nalix.Abstractions;

namespace Nalix.Network.Internal.Protocol;

/// <summary>
/// Carries all state needed for one Proxy Protocol handshake.
/// Pooled to avoid heap allocation per connection during SYN floods.
/// Doubles as an intrusive doubly-linked list node for the O(k) timeout sweep.
/// </summary>
internal sealed class ProxyHandshakeState : IPoolable
{
    // ── Socket ───────────────────────────────────────────────────────────────
    public Socket? Socket;

    public byte[]? Buffer;
    public int BytesReceived;

    // ── Timeout tracking ─────────────────────────────────────────────────────
    public long HandshakeStartTimeTicks;

    // ── Intrusive doubly-linked list — zero-allocation timeout sweep ─────────
    public ProxyHandshakeState? Next;
    public ProxyHandshakeState? Prev;

    public bool RemovedFromList { get; set; }

    public void ResetForPool()
    {
        Socket = null;
        Buffer = null;
        BytesReceived = 0;
        HandshakeStartTimeTicks = 0;
        Next = null;
        Prev = null;

        this.RemovedFromList = false;
    }
}
