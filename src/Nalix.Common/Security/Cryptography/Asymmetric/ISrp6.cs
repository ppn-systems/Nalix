namespace Nalix.Common.Security.Cryptography.Asymmetric;

/// <summary>
/// Interface for SRP-6 encryption and authentication methods.
/// </summary>
public interface ISrp6
{
    /// <summary>
    /// Creates server credentials to send to the client.
    /// </summary>
    /// <returns>Server credentials as a byte array.</returns>
    System.Byte[] GenerateServerCredentials();

    /// <summary>
    /// Processes the client's authentication information. If valid, the shared secret is generated.
    /// </summary>
    /// <param name="clientPublicValueBytes">The client's public value as a byte array.</param>
    void CalculateSecret(System.Byte[] clientPublicValueBytes);

    /// <summary>
    /// Calculates the session key from the shared secret.
    /// </summary>
    /// <returns>Session key as a byte array.</returns>
    System.Byte[] CalculateSessionKey();

    /// <summary>
    /// Validates the client proof message and saves it if it is correct.
    /// </summary>
    /// <param name="clientProofMessage">The client proof message as a byte array.</param>
    /// <returns>True if the client proof message is valid, otherwise false.</returns>
    System.Boolean VerifyClientEvidenceMessage(System.Byte[] clientProofMessage);

    /// <summary>
    /// Computes the server proof message using previously verified values.
    /// </summary>
    /// <returns>The server proof message as a byte array.</returns>
    System.Byte[] CalculateServerEvidenceMessage();
}
