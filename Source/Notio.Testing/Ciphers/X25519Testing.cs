using Notio.Cryptography.Ciphers.Asymmetric;
using System;

namespace Notio.Testing.Ciphers;

public static class X25519Testing
{
    public static void Main()
    {
        try
        {
            TestKeyPairGeneration();
            TestSharedSecretComputation();
            Console.WriteLine("");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nTest failed: {ex.Message}");
        }
    }

    private static void TestKeyPairGeneration()
    {
        var (privateKey, publicKey) = X25519.GenerateKeyPair();

        if (privateKey == null || privateKey.Length != 32)
        {
            Console.WriteLine("❌ Private key length is incorrect.");
            return;
        }

        if (publicKey == null || publicKey.Length != 32)
        {
            Console.WriteLine("Public key length is incorrect.");
            return;
        }

        Console.WriteLine("TestKeyPairGeneration: Passed");
    }

    private static void TestSharedSecretComputation()
    {
        var (privateKeyA, publicKeyA) = X25519.GenerateKeyPair();
        var (privateKeyB, publicKeyB) = X25519.GenerateKeyPair();

        byte[] sharedSecret1 = X25519.ComputeSharedSecret(privateKeyA, publicKeyB);
        byte[] sharedSecret2 = X25519.ComputeSharedSecret(privateKeyB, publicKeyA);

        if (sharedSecret1 == null || sharedSecret1.Length != 32 ||
            sharedSecret2 == null || sharedSecret2.Length != 32)
        {
            Console.WriteLine("Shared secret length is incorrect.");
            return;
        }

        if (!AreEqual(sharedSecret1, sharedSecret2))
        {
            Console.WriteLine("Shared secrets do not match.");
            return;
        }

        Console.WriteLine("TestSharedSecretComputation: Passed");
    }

    private static bool AreEqual(byte[] a, byte[] b)
    {
        if (a == null || b == null || a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i]) return false;
        }
        return true;
    }
}