// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Shared.Messaging.Control;

/// <summary>
/// Represents the type of control message used in messaging operations.
/// </summary>
public enum ControlType : System.Byte
{
    /// <summary>
    /// Represents a ping message used to check the connection status.
    /// </summary>
    Ping = 0x01,

    /// <summary>
    /// Represents a pong message sent in response to a ping.
    /// </summary>
    Pong = 0x02,

    /// <summary>
    /// Represents an acknowledgment message confirming receipt of a message.
    /// </summary>
    Ack = 0x03,

    /// <summary>
    /// Represents a disconnect message indicating the termination of a connection.
    /// </summary>
    Disconnect = 0x04
}