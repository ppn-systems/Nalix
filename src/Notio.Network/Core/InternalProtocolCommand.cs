namespace Notio.Network.Core;

/// <summary>
/// Defines the protocol commands used for managing secure connection processes.
/// These commands are part of the protocol for establishing and finalizing encrypted connections.
/// </summary>
public enum InternalProtocolCommand : ushort
{
    /// <summary>
    /// Number to initiate the handshake by requesting the server's public key.
    /// This command begins the process of securely establishing a connection.
    /// </summary>
    StartHandshake = 0x0001,

    /// <summary>
    /// Number to complete the handshake by finalizing the secure connection.
    /// This command verifies the client's public key and finalizes the encryption key exchange.
    /// </summary>
    CompleteHandshake = 0x0002,

    /// <summary>
    /// Command to set the compression mode for the connection.
    /// </summary>
    SetCompressionMode = 0x0003,

    /// <summary>
    /// Command to set the encryption mode for the connection.
    /// </summary>
    SetEncryptionMode = 0x0004,

    /// <summary>
    /// Sent by the client to gracefully terminate the connection with the server.
    /// This allows for a clean shutdown without triggering unexpected disconnect handling.
    /// </summary>
    Disconnect = 0x0005,

    /// <summary>
    /// Sent by the client to request current connection status information such as compression or encryption settings.
    /// </summary>
    ConnectionStatus = 0x0006,

    /// <summary>
    /// Sent by the client to check if the server is alive and measure latency.
    /// </summary>
    Ping = 0x0007,

    /// <summary>
    /// Sent by the server in response to a Ping request.
    /// </summary>
    Pong = 0x0008,

    /// <summary>
    /// Sent by the client to request the server's current ping time.
    /// </summary>
    PingTime = 0x0009
}
