using Notio.Cryptography.Ciphers.Symmetric;
using System;
using System.Linq;
using System.Text;

namespace Notio.Testing.Ciphers;

public static class ChaCha20Poly1305Testing
{
    public static void Main()
    {
        try
        {
            TestEncryptionAndDecryption();
            TestAuthenticationFailure();
        }
        catch
        {
        }

        Console.WriteLine("");
    }

    private static void TestEncryptionAndDecryption()
    {
        byte[] key = new byte[32];
        byte[] nonce = new byte[12];
        byte[] plaintext = Encoding.UTF8.GetBytes("Hello, ChaCha20-Poly1305!");
        byte[] aad = Encoding.UTF8.GetBytes("AdditionalData");

        ChaCha20Poly1305.Encrypt(key, nonce, plaintext, aad, out byte[] ciphertext, out byte[] tag);

        if (ciphertext.Length != plaintext.Length)
            throw new Exception("Ciphertext length is incorrect.");

        if (tag.Length != 16)
            throw new Exception("Authentication tag length is incorrect.");

        bool success = ChaCha20Poly1305.Decrypt(key, nonce, ciphertext, aad, tag, out byte[] decrypted);

        if (!success)
            throw new Exception("Decryption failed.");

        if (!decrypted.SequenceEqual(plaintext))
            throw new Exception("Decrypted text does not match the original.");

        Console.WriteLine("TestEncryptionAndDecryption: Passed");
    }

    private static void TestAuthenticationFailure()
    {
        byte[] key = new byte[32];
        byte[] nonce = new byte[12];
        byte[] plaintext = Encoding.UTF8.GetBytes("Sensitive Data");
        byte[] aad = Encoding.UTF8.GetBytes("AAD");

        ChaCha20Poly1305.Encrypt(key, nonce, plaintext, aad, out byte[] ciphertext, out byte[] tag);

        // Modify the tag slightly to simulate an authentication failure.
        tag[0] ^= 0xFF;

        bool success = ChaCha20Poly1305.Decrypt(key, nonce, ciphertext, aad, tag, out _);

        if (success)
            throw new Exception("Authentication failure test failed. Decryption should have failed.");

        Console.WriteLine("TestAuthenticationFailure: Passed");
    }
}