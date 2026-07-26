// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Network.Internal.Tcp;

/// <summary>
/// Represents the result of accepting an inbound TCP connection.
/// </summary>
internal enum AcceptConnectionResult
{
    /// <summary>
    /// The connection was accepted successfully.
    /// </summary>
    Accepted,

    /// <summary>
    /// The accepted socket was invalid or unusable.
    /// </summary>
    InvalidSocket,

    /// <summary>
    /// The connection was rejected by the connection limiter.
    /// </summary>
    RejectedByLimiter,

    /// <summary>
    /// The connection could not be queued because the processing channel was full.
    /// </summary>
    ProcessChannelFull,

    /// <summary>
    /// The listener was closed before the connection could be accepted.
    /// </summary>
    ListenerClosed,

    /// <summary>
    /// The socket accept operation was aborted.
    /// </summary>
    SocketAborted,

    /// <summary>
    /// The connection accept operation failed for an unspecified reason.
    /// </summary>
    Failed,

    /// <summary>
    /// The connection is pending asynchronous initialization (e.g. WebSocket handshake).
    /// </summary>
    Pending
}
