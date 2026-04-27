// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Abstractions.Networking.Protocols;

/// <summary>
/// Identifies the kind of control message used by the protocol layer.
/// </summary>
public enum ControlType : byte
{
    /// <summary>
    /// No control message specified.
    /// </summary>
    NONE = 0x00,

    /// <summary>
    /// Ping message used to check connection liveness.
    /// </summary>
    PING = 0x01,

    /// <summary>
    /// Pong response sent to a ping.
    /// </summary>
    PONG = 0x02,

    /// <summary>
    /// Acknowledgement confirming receipt.
    /// </summary>
    ACK = 0x03,

    /// <summary>
    /// Disconnect request or notification.
    /// </summary>
    DISCONNECT = 0x04,

    /// <summary>
    /// Error message describing a failure.
    /// </summary>
    ERROR = 0x05,

    /// <summary>
    /// Heartbeat message used to keep a connection active.
    /// </summary>
    HEARTBEAT = 0x07,

    /// <summary>
    /// Negative acknowledgement indicating that processing failed.
    /// </summary>
    NACK = 0x08,

    /// <summary>
    /// Resume a previously interrupted connection or session.
    /// </summary>
    RESUME = 0x09,

    /// <summary>
    /// Request graceful shutdown.
    /// </summary>
    SHUTDOWN = 0x0A,

    /// <summary>
    /// Instruct the client to redirect to another endpoint.
    /// </summary>
    REDIRECT = 0x0B,

    /// <summary>
    /// Request the client to reduce its rate.
    /// </summary>
    THROTTLE = 0x0C,

    /// <summary>
    /// Maintenance notice sent by the server.
    /// </summary>
    NOTICE = 0x0D,

    /// <summary>
    /// The operation timed out.
    /// </summary>
    TIMEOUT = 0x10,

    /// <summary>
    /// Generic operation failure.
    /// </summary>
    FAIL = 0x11,

    /// <summary>
    /// Client requests server time.
    /// </summary>
    TIMESYNCREQUEST = 0x12,

    /// <summary>
    /// Server responds with its current time.
    /// </summary>
    TIMESYNCRESPONSE = 0x13,

    /// <summary>
    /// Request to change the connection's cipher suite algorithm.
    /// </summary>
    CIPHER_UPDATE = 0x14,

    /// <summary>
    /// Acknowledges the cipher suite update.
    /// </summary>
    CIPHER_UPDATE_ACK = 0x15,

    /// <summary>
    /// Reserved for future extension.
    /// </summary>
    RESERVED1 = 0xFE,

    /// <summary>
    /// Reserved for future extension.
    /// </summary>
    RESERVED2 = 0xFF
}
