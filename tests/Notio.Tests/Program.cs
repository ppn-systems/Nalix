using Notio.Cryptography.Asymmetric;
using System;
using System.Collections;

namespace Notio.Tests;

public class Program
{
    public static void Main(string[] args)
    {
        // Generate key pairs for two parties
        var (PrivateKey, PublicKey) = X25519.GenerateKeyPair();
        var bobKeyPair = X25519.GenerateKeyPair();

        // Print Alice's and Bob's public keys
        Console.WriteLine("Alice's Public Key: " + Convert.ToBase64String(PublicKey));
        Console.WriteLine("Bob's Public Key: " + Convert.ToBase64String(bobKeyPair.PublicKey));

        // Compute shared secrets
        var aliceSharedSecret = X25519.ComputeSharedSecret(PrivateKey, bobKeyPair.PublicKey);
        var bobSharedSecret = X25519.ComputeSharedSecret(bobKeyPair.PrivateKey, PublicKey);

        // Print shared secrets
        Console.WriteLine("Alice's Shared Secret: " + Convert.ToBase64String(aliceSharedSecret));
        Console.WriteLine("Bob's Shared Secret: " + Convert.ToBase64String(bobSharedSecret));

        // Verify that the shared secrets match
        if (StructuralComparisons.StructuralEqualityComparer.Equals(aliceSharedSecret, bobSharedSecret))
        {
            Console.WriteLine("Shared secrets match! Key exchange was successful.");
        }
        else
        {
            Console.WriteLine("Shared secrets do NOT match! Key exchange failed.");
        }
    }
}
