using System;
using Nalix.Common.Security;
using Nalix.Framework.Security;

public class Program {
    public static void Main() {
        var key = new byte[32];
        var plaintext = new byte[100];
        var aad = new byte[] { 1, 2, 3, 4 };
        var ciphertext = new byte[1000];
        
        EnvelopeCipher.Encrypt(key, plaintext, ciphertext, aad, null, CipherSuiteType.Chacha20Poly1305, out int written);
        
        var decrypted = new byte[100];
        try {
            EnvelopeCipher.Decrypt(key, ciphertext.AsSpan()[..written], decrypted, aad, out int decWritten);
            Console.WriteLine("Decryption successful! Written: " + decWritten);
        } catch (Exception ex) {
            Console.WriteLine("Decryption Failed: " + ex);
        }
    }
}
