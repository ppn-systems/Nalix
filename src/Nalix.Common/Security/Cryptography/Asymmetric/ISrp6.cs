namespace Nalix.Common.Security.Cryptography.Asymmetric;

/// <summary>
/// Defines the operations for the SRP-6 (Secure Remote Password) protocol, 
/// which provides password-based authentication and key exchange without sending the password over the network.
/// </summary>
public interface ISrp6
{
    /// <summary>
    /// Generates the server's public credentials (<c>B</c>) to send to the client during the handshake phase.
    /// </summary>
    /// <returns>
    /// A byte array containing the server's public value, used by the client to compute the shared secret.
    /// </returns>
    System.Byte[] GenerateServerCredentials();

    /// <summary>
    /// Processes the client's public value (<c>A</c>) and computes the shared secret (<c>S</c>).
    /// </summary>
    /// <param name="clientPublicValueBytes">
    /// The client's public value (<c>A</c>) as a byte array.
    /// </param>
    /// <remarks>
    /// This method must be called before <see cref="CalculateSessionKey"/> to establish the shared secret.
    /// </remarks>
    void CalculateSecret(System.Byte[] clientPublicValueBytes);

    /// <summary>
    /// Derives the session key (<c>K</c>) from the shared secret (<c>S</c>).
    /// </summary>
    /// <returns>
    /// A byte array containing the session key, used for encrypting further communication.
    /// </returns>
    System.Byte[] CalculateSessionKey();

    /// <summary>
    /// Verifies the client's proof message (<c>M1</c>) to ensure that both client and server have the same session key.
    /// </summary>
    /// <param name="clientProofMessage">
    /// The client's proof message (<c>M1</c>) as a byte array.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the proof is valid; otherwise, <see langword="false"/>.
    /// </returns>
    System.Boolean VerifyClientEvidenceMessage(System.Byte[] clientProofMessage);

    /// <summary>
    /// Computes the server's proof message (<c>M2</c>) for the client, confirming that the server has verified the client's proof.
    /// </summary>
    /// <returns>
    /// A byte array containing the server's proof message (<c>M2</c>).
    /// </returns>
    System.Byte[] CalculateServerEvidenceMessage();
}
