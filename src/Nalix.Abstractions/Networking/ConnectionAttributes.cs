// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Abstractions.Networking;

/// <summary>
/// Provides a centralized set of well-known connection attribute keys used throughout the Nalix framework.
/// </summary>
public static class ConnectionAttributes
{
    /// <summary>
    /// Key for the connection hub that owns this connection.
    /// </summary>
    public static readonly AttributeKey OwnerHub = AttributeKey.FromName("nalix.owner_hub");

    /// <summary>
    /// Stores the last TCP send sequence number (for session resume).
    /// </summary>
    public static readonly AttributeKey TcpSendSequence = AttributeKey.FromName("nalix.tcp.sequence.send");

    /// <summary>
    /// Stores the last TCP receive sequence number (for session resume).
    /// </summary>
    public static readonly AttributeKey TcpReceiveSequence = AttributeKey.FromName("nalix.tcp.sequence.receive");

    /// <summary>
    /// Stores the last UDP send sequence number (for session resume).
    /// </summary>
    public static readonly AttributeKey UdpSendSequence = AttributeKey.FromName("nalix.udp.sequence.send");

    /// <summary>
    /// Stores the last UDP receive sequence number (for session resume).
    /// </summary>
    public static readonly AttributeKey UdpReceiveSequence = AttributeKey.FromName("nalix.udp.sequence.receive");

    /// <summary>
    /// Key for the handshake context state stored during the negotiation process.
    /// </summary>
    public static readonly AttributeKey HandshakeState = AttributeKey.FromName("nalix.handshake.state");

    /// <summary>
    /// Key for the boolean attribute indicating whether a handshake has been successfully established.
    /// </summary>
    public static readonly AttributeKey HandshakeEstablished = AttributeKey.FromName("nalix.handshake.established");

    /// <summary>
    /// Synchronization key used to coordinate anti-spam directive send guards per connection.
    /// </summary>
    public static readonly AttributeKey InboundDirectiveGuardLock = AttributeKey.FromName("nalix.inbound.directive.guard.lock");

    /// <summary>
    /// Stores the last monotonic timestamp (ms) when a TIMEOUT directive was sent.
    /// </summary>
    public static readonly AttributeKey InboundDirectiveTimeoutLastSentAtMs = AttributeKey.FromName("nalix.inbound.directive.timeout.last_sent_at_ms");

    /// <summary>
    /// Stores the last monotonic timestamp (ms) when a RATE_LIMITED directive was sent.
    /// Shared by rate-limit and concurrency middleware to avoid duplicate bursts.
    /// </summary>
    public static readonly AttributeKey InboundDirectiveRateLimitedLastSentAtMs = AttributeKey.FromName("nalix.inbound.directive.rate_limited.last_sent_at_ms");

    /// <summary>
    /// Stores the last monotonic timestamp (ms) when an UNAUTHORIZED directive was sent.
    /// </summary>
    public static readonly AttributeKey InboundDirectiveUnauthorizedLastSentAtMs = AttributeKey.FromName("nalix.inbound.directive.unauthorized.last_sent_at_ms");

    /// <summary>
    /// Stores the last monotonic timestamp (ms) when a control log entry was emitted.
    /// Prevents log flooding from repeated ERROR/FAIL/NOTICE packets.
    /// </summary>
    public static readonly AttributeKey InboundControlLogLastSentAtMs = AttributeKey.FromName("nalix.inbound.control.log.last_sent_at_ms");
}
