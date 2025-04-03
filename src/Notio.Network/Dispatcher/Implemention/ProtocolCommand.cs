namespace Notio.Network.Dispatcher.Implemention;

/// <summary>
/// Defines the protocol commands used for managing secure connection processes.
/// These commands are part of the protocol for establishing and finalizing encrypted connections.
/// </summary>
public enum ProtocolCommand : ushort
{
    /// <summary>
    /// Command to initiate the handshake by requesting the server's public key.
    /// This command begins the process of securely establishing a connection.
    /// </summary>
    InitiateHandshake = 0x0001,

    /// <summary>
    /// Command to complete the handshake by finalizing the secure connection.
    /// This command verifies the client's public key and finalizes the encryption key exchange.
    /// </summary>
    CompleteHandshake = 0x0002
}
