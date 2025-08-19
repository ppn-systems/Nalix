// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Connection.Protocols;

/// <summary>
/// Defines standard reason codes for control packets such as Disconnect, Error, or Nack.
/// </summary>
public enum ReasonCode : System.UInt16
{
    /// <summary>
    /// No reason specified (default).
    /// </summary>
    None = 0,

    /// <summary>
    /// The operation timed out.
    /// </summary>
    Timeout = 1,

    /// <summary>
    /// The client or server was not authorized.
    /// </summary>
    Unauthorized = 2,

    /// <summary>
    /// The server is shutting down intentionally.
    /// </summary>
    ServerShutdown = 3,

    /// <summary>
    /// The connection was closed by the client.
    /// </summary>
    ClientQuit = 4,

    /// <summary>
    /// The packet format or protocol version was invalid or unsupported.
    /// </summary>
    ProtocolError = 5,

    /// <summary>
    /// The client was explicitly disconnected (e.g., kicked).
    /// </summary>
    Kicked = 6
}
