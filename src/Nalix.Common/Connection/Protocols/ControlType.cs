// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Connection.Protocols;

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
    SHUTDOWN = 0x0A
}