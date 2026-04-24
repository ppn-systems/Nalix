// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Common.Networking;

/// <summary>
/// Provides a centralized set of well-known connection attribute keys used throughout the Nalix framework.
/// </summary>
public static class ConnectionAttributes
{
    /// <summary>
    /// Key for the handshake context state stored during the negotiation process.
    /// </summary>
    public const string HandshakeState = "nalix.handshake.state";

    /// <summary>
    /// Key for the boolean attribute indicating whether a handshake has been successfully established.
    /// </summary>
    public const string HandshakeEstablished = "nalix.handshake.established";

    /// <summary>
    /// Synchronization key used to coordinate anti-spam directive send guards per connection.
    /// </summary>
    public const string InboundDirectiveGuardSyncRoot = "nalix.inbound.directive.guard.sync_root";

    /// <summary>
    /// Stores the last monotonic timestamp (ms) when a TIMEOUT directive was sent.
    /// </summary>
    public const string InboundDirectiveTimeoutLastSentAtMs = "nalix.inbound.directive.timeout.last_sent_at_ms";

    /// <summary>
    /// Stores the last monotonic timestamp (ms) when a RATE_LIMITED directive was sent.
    /// Shared by rate-limit and concurrency middleware to avoid duplicate bursts.
    /// </summary>
    public const string InboundDirectiveRateLimitedLastSentAtMs = "nalix.inbound.directive.rate_limited.last_sent_at_ms";

    /// <summary>
    /// Stores the last monotonic timestamp (ms) when an UNAUTHORIZED directive was sent.
    /// </summary>
    public const string InboundDirectiveUnauthorizedLastSentAtMs = "nalix.inbound.directive.unauthorized.last_sent_at_ms";
}
