namespace Nalix.Common.Security.Cryptography.Asymmetric;

/// <summary>
/// Interface for X25519 elliptic curve Diffie-Hellman (ECDH) key exchange operations.
/// </summary>
public interface IX25519
{
    /// <summary>
    /// Generates an X25519 key pair.
    /// </summary>
    /// <returns>A tuple with (privateKey, publicKey) each 32 bytes.</returns>
    (System.Byte[] PrivateKey, System.Byte[] PublicKey) Generate();

    /// <summary>
    /// Computes the shared secret between your private key and a peer's public key.
    /// </summary>
    /// <param name="privateKey">Your 32-byte private key.</param>
    /// <param name="peerPublicKey">The peer's 32-byte public key.</param>
    /// <returns>The shared secret as a 32-byte array.</returns>
    System.Byte[] Compute(System.ReadOnlySpan<System.Byte> privateKey, System.ReadOnlySpan<System.Byte> peerPublicKey);
}
