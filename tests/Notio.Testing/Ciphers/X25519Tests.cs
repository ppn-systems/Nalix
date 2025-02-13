using Notio.Cryptography.Ciphers.Asymmetric;
using System;
using Xunit;

namespace Notio.Testing.Ciphers
{
    public class X25519Tests
    {
        [Fact]
        public void TestKeyPairGeneration()
        {
            var (privateKey, publicKey) = X25519.GenerateKeyPair();

            // Assert that private key is not null and has correct length
            Assert.NotNull(privateKey);
            Assert.Equal(32, privateKey.Length);

            // Assert that public key is not null and has correct length
            Assert.NotNull(publicKey);
            Assert.Equal(32, publicKey.Length);

            Console.WriteLine("TestKeyPairGeneration: Passed");
        }

        [Fact]
        public void TestSharedSecretComputation()
        {
            var (privateKeyA, publicKeyA) = X25519.GenerateKeyPair();
            var (privateKeyB, publicKeyB) = X25519.GenerateKeyPair();

            byte[] sharedSecret1 = X25519.ComputeSharedSecret(privateKeyA, publicKeyB);
            byte[] sharedSecret2 = X25519.ComputeSharedSecret(privateKeyB, publicKeyA);

            // Assert that the shared secrets are not null and have the correct length
            Assert.NotNull(sharedSecret1);
            Assert.Equal(32, sharedSecret1.Length);
            Assert.NotNull(sharedSecret2);
            Assert.Equal(32, sharedSecret2.Length);

            // Assert that the shared secrets match
            Assert.True(AreEqual(sharedSecret1, sharedSecret2), "Shared secrets do not match.");

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
}
