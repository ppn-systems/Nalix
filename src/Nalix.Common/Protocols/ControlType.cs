// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Protocols;

/// <summary>
/// Represents the type of control message used in messaging operations.
/// </summary>
public enum ControlType : System.Byte
{
    /// <summary>
    /// Represents a null or uninitialized control message.
    /// </summary>
    NONE = 0x00,

    /// <summary>
    /// Represents a ping message used to check the connection status.
    /// </summary>
    PING = 0x01,

    /// <summary>
    /// Represents a pong message sent in response to a ping.
    /// </summary>
    PONG = 0x02,

    /// <summary>
    /// Represents an acknowledgment message confirming receipt of a message.
    /// </summary>
    ACK = 0x03,

    /// <summary>
    /// Represents a disconnect message indicating the termination of a connection.
    /// </summary>
    DISCONNECT = 0x04,

    /// <summary>
    /// Represents an error message indicating a problem or failure in the connection.
    /// </summary>
    ERROR = 0x05,

    /// <summary>
    /// Represents a handshake message used to initiate or negotiate a connection.
    /// </summary>
    HANDSHAKE = 0x06,

    /// <summary>
    /// Represents a heartbeat message used to maintain an active connection.
    /// </summary>
    HEARTBEAT = 0x07,

    /// <summary>
    /// Represents a negative acknowledgment message indicating that a message was not received or processed successfully.
    /// </summary>
    NACK = 0x08,

    /// <summary>
    /// Represents a resume message used to continue a previously interrupted connection or session.
    /// </summary>
    RESUME = 0x09,

    /// <summary>
    /// Represents a shutdown message indicating a request to gracefully terminate the connection or service.
    /// </summary>
    SHUTDOWN = 0x0A,

    /// <summary>
    /// Represents a message instructing the client to redirect to another endpoint.
    /// </summary>
    REDIRECT = 0x0B,

    /// <summary>
    /// Represents a throttling message requesting the client to reduce its rate.
    /// </summary>
    THROTTLE = 0x0C,

    /// <summary>
    /// Represents a maintenance notice sent by the server.
    /// </summary>
    NOTICE = 0x0D,

    /// <summary>
    /// The operation has timed out.
    /// </summary>
    TIMEOUT = 0x10,

    /// <summary>
    /// Failure in processing the operation.
    /// </summary>
    FAIL = 0x11,

    /// <summary>
    /// Client requests server time.
    /// </summary>
    TIME_SYNC_REQUEST = 0x12,

    /// <summary>
    /// Server responds with server timestamp.
    /// </summary>
    TIME_SYNC_RESPONSE = 0x13,

    /// <summary>
    /// Reserved for future extension.
    /// </summary>
    RESERVED1 = 0xFE,

    /// <summary>
    /// Reserved for future extension.
    /// </summary>
    RESERVED2 = 0xFF
}