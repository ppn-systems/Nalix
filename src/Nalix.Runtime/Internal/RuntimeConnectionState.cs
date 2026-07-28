// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Threading;

namespace Nalix.Runtime.Internal;

/// <summary>
/// Maintains runtime-specific state for a connection, such as handshake status and directive throttling lock.
/// Pooled to avoid allocations.
/// </summary>
internal sealed class RuntimeConnectionState
{
    /// <summary>
    /// Gets or sets the handshake context state stored during the negotiation process.
    /// </summary>
    public object? HandshakeState;

    /// <summary>
    /// Gets or sets a value indicating whether a handshake has been successfully established.
    /// </summary>
    public bool HandshakeEstablished;

    /// <summary>
    /// Stores the last monotonic timestamp (ms) when a TIMEOUT directive was sent.
    /// </summary>
    public long InboundDirectiveTimeoutLastSentAtMs;

    /// <summary>
    /// Stores the last monotonic timestamp (ms) when a RATE_LIMITED directive was sent.
    /// Shared by rate-limit and concurrency middleware to avoid duplicate bursts.
    /// </summary>
    public long InboundDirectiveRateLimitedLastSentAtMs;

    /// <summary>
    /// Stores the last monotonic timestamp (ms) when an UNAUTHORIZED directive was sent.
    /// </summary>
    public long InboundDirectiveUnauthorizedLastSentAtMs;

    /// <summary>
    /// Stores the last monotonic timestamp (ms) when a control log entry was emitted.
    /// Prevents log flooding from repeated ERROR/FAIL/NOTICE packets.
    /// </summary>
    public long InboundControlLogLastSentAtMs;

    /// <summary>
    /// Synchronization lock used to coordinate anti-spam directive send guards per connection.
    /// </summary>
    public Lock DirectiveGuardLock { get; } = new();
}
