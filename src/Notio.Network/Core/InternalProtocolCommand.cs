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
}
