using Notio.Cryptography.Ciphers.Symmetric;
using System;
using System.Linq;
using System.Text;
using Xunit;

namespace Notio.Testing.Ciphers;

public class ChaCha20Poly1305Tests
{
    [Fact]
    public void TestEncryptionAndDecryption()
    {
        byte[] key = new byte[32];
        byte[] nonce = new byte[12];
        byte[] plaintext = Encoding.UTF8.GetBytes("Hello, ChaCha20-Poly1305!");
        byte[] aad = Encoding.UTF8.GetBytes("AdditionalData");

        // Encrypt the plaintext
        ChaCha20Poly1305.Encrypt(key, nonce, plaintext, aad, out byte[] ciphertext, out byte[] tag);

        // Assert the ciphertext length is the same as plaintext length
        Assert.Equal(plaintext.Length, ciphertext.Length);

        // Assert the tag length is 16 bytes (as expected in ChaCha20-Poly1305)
        Assert.Equal(16, tag.Length);

        // Decrypt the ciphertext
        bool success = ChaCha20Poly1305.Decrypt(key, nonce, ciphertext, aad, tag, out byte[] decrypted);

        // Assert decryption is successful
        Assert.True(success, "Decryption failed.");

        // Assert that the decrypted text matches the original plaintext
        Assert.True(decrypted.SequenceEqual(plaintext), "Decrypted text does not match the original.");
    }

    [Fact]
    public void TestAuthenticationFailure()
    {
        byte[] key = new byte[32];
        byte[] nonce = new byte[12];
        byte[] plaintext = Encoding.UTF8.GetBytes("Sensitive Data");
        byte[] aad = Encoding.UTF8.GetBytes("AAD");

        // Encrypt the plaintext
        ChaCha20Poly1305.Encrypt(key, nonce, plaintext, aad, out byte[] ciphertext, out byte[] tag);

        // Modify the tag slightly to simulate an authentication failure
        tag[0] ^= 0xFF;

        // Try to decrypt with the modified tag
        bool success = ChaCha20Poly1305.Decrypt(key, nonce, ciphertext, aad, tag, out _);

        // Assert that decryption fails
        Assert.False(success, "Authentication failure test failed. Decryption should have failed.");
    }
}
