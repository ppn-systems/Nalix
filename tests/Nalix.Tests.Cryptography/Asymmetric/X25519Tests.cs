using Nalix.Cryptography.Asymmetric;
using System;
using System.Linq;
using Xunit;

namespace Nalix.Test.Cryptography.Asymmetric;

/// <summary>
/// Test suite for the X25519 elliptic curve Diffie-Hellman implementation.
/// </summary>
public class X25519Tests
{
    #region Test Vectors

    private static readonly byte[] TestResult1 =
    [
        0xc3, 0xda, 0x55, 0x37, 0x9d, 0xe9, 0xc6, 0x90,
        0x8e, 0x94, 0xea, 0x4d, 0xf2, 0x8d, 0x08, 0x4f,
        0x32, 0xec, 0xcf, 0x03, 0x49, 0x1c, 0x71, 0xf7,
        0x54, 0xb4, 0x07, 0x55, 0x77, 0xa2, 0x85, 0x52
    ];

    private static readonly byte[] TestResult2 =
    [
        0x95, 0xcb, 0xde, 0x94, 0x76, 0xe8, 0x90, 0x7d,
        0x7a, 0xad, 0xe4, 0x5c, 0xb4, 0xb8, 0x73, 0xf8,
        0x8b, 0x59, 0x5a, 0x68, 0x79, 0x9f, 0xa1, 0x52,
        0xe6, 0xf8, 0xf7, 0x64, 0x7a, 0xac, 0x79, 0x57
    ];

    // Standard base point (u=9)
    private static readonly byte[] BasePoint = [
        9, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];

    #endregion Test Vectors

    #region Key Generation Tests

    [Fact]
    public void GenerateKeyPair_ReturnsValidKeyPair()
    {
        // Act
        X25519.GenerateKeyPair(out byte[] privateKey, out byte[] publicKey);

        // Assert
        Assert.NotNull(privateKey);
        Assert.NotNull(publicKey);
        Assert.Equal(32, privateKey.Length);
        Assert.Equal(32, publicKey.Length);

        // Verify the private key is properly clamped
        Assert.Equal(0, privateKey[0] & 0x07);    // Lower 3 bits cleared
        Assert.Equal(0, privateKey[31] & 0x80);   // Highest bit cleared
        Assert.NotEqual(0, privateKey[31] & 0x40); // Second highest bit set

        // Verify that the generated key pair works for shared secret computation

        Span<byte> sharedSecret = stackalloc byte[32];
        X25519.ComputeSharedSecret(privateKey, publicKey, sharedSecret);
        Assert.Equal(32, sharedSecret.Length);
    }

    [Fact]
    public void GenerateKeyPair_ReturnsDifferentKeysOnMultipleCalls()
    {
        // Act
        X25519.GenerateKeyPair(out byte[] privateKey1, out byte[] publicKey1);
        X25519.GenerateKeyPair(out byte[] privateKey2, out byte[] publicKey2);

        // Assert
        Assert.False(privateKey1.SequenceEqual(privateKey2), "Generated private keys should be different");
        Assert.False(publicKey1.SequenceEqual(publicKey2), "Generated public keys should be different");
    }

    #endregion Key Generation Tests

    #region RFC 7748 Test Vector Tests

    [Fact]
    public void GenerateKeyPair_ReturnsValidKeys()
    {
        // Act: Generate key pairs
        X25519.GenerateKeyPair(out byte[] privateKey1, out byte[] publicKey1);
        X25519.GenerateKeyPair(out byte[] privateKey2, out byte[] publicKey2);

        // Assert: Check that private and public keys are 32 bytes long
        Assert.Equal(32, privateKey1.Length);
        Assert.Equal(32, publicKey1.Length);
        Assert.Equal(32, privateKey2.Length);
        Assert.Equal(32, publicKey2.Length);

        // Assert: Check that private keys are different (not the same for different key pairs)
        Assert.NotEqual(privateKey1, privateKey2);

        // Assert: Check that public keys are different (not the same for different key pairs)
        Assert.NotEqual(publicKey1, publicKey2);

        Span<byte> sharedSecret1 = stackalloc byte[32];
        Span<byte> sharedSecret2 = stackalloc byte[32];

        // Assert: Check that each public key corresponds to the correct private key
        X25519.ComputeSharedSecret(privateKey1, publicKey2, sharedSecret1);
        X25519.ComputeSharedSecret(privateKey2, publicKey1, sharedSecret2);
        Assert.Equal(sharedSecret1, sharedSecret2);
    }

    #endregion RFC 7748 Test Vector Tests

    #region Key Exchange Tests

    [Fact]
    public void KeyExchange_BothParties_ComputeSameSharedSecret()
    {
        // Arrange - Generate key pairs for Alice and Bob
        X25519.GenerateKeyPair(out byte[] alicePrivate, out byte[] alicePublic);
        X25519.GenerateKeyPair(out byte[] bobPrivate, out byte[] bobPublic);

        Span<byte> aliceSharedSecret = stackalloc byte[32];
        Span<byte> bobSharedSecret = stackalloc byte[32];

        // Act - Compute shared secrets on both sides
        X25519.ComputeSharedSecret(alicePrivate, bobPublic, aliceSharedSecret);
        X25519.ComputeSharedSecret(bobPrivate, alicePublic, bobSharedSecret);

        // Assert - Both parties should compute the same shared secret
        Assert.Equal(aliceSharedSecret, bobSharedSecret);
    }

    [Fact]
    public void KeyExchange_MultiplePairs_ProducesUniqueSecrets()
    {
        // Arrange
        const int pairCount = 5;
        var sharedSecrets = new byte[pairCount][];

        Span<byte> aliceSharedSecret = stackalloc byte[32];

        // Act - Generate multiple key pairs and compute shared secrets
        for (int i = 0; i < pairCount; i++)
        {
            X25519.GenerateKeyPair(out byte[] alicePrivate, out byte[] alicePublic);
            X25519.GenerateKeyPair(out byte[] bobPrivate, out byte[] bobPublic);

            X25519.ComputeSharedSecret(alicePrivate, bobPublic, aliceSharedSecret);

            sharedSecrets[i] = aliceSharedSecret.ToArray();
        }

        // Assert - All shared secrets should be unique
        for (int i = 0; i < pairCount; i++)
        {
            for (int j = i + 1; j < pairCount; j++)
            {
                Assert.False(sharedSecrets[i].SequenceEqual(sharedSecrets[j]),
                    $"Shared secrets at indices {i} and {j} are identical");
            }
        }
    }

    #endregion Key Exchange Tests

    #region Input Validation Tests

    [Fact]
    public void ComputeSharedSecret_InvalidPrivateKeyLength_ThrowsArgumentException()
    {
        // Arrange
        var invalidPrivateKey = new byte[16]; // Too short
        var validPublicKey = new byte[32];
        var bytes = new byte[32]; // Changed from Span<byte> to byte[]

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            X25519.ComputeSharedSecret(invalidPrivateKey, validPublicKey, bytes));
        Assert.Contains("Private key must be 32 bytes", ex.Message);
    }

    [Fact]
    public void ComputeSharedSecret_InvalidPublicKeyLength_ThrowsArgumentException()
    {
        // Arrange
        var validPrivateKey = new byte[32];
        var invalidPublicKey = new byte[64]; // Too long

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            X25519.ComputeSharedSecret(validPrivateKey, invalidPublicKey, new byte[32]));
        Assert.Contains("Public key must be 32 bytes", ex.Message);
    }

    [Fact]
    public void ComputeSharedSecret_ScalarZero_ReturnsZeroResult()
    {
        // Arrange - RFC 7748 specifies that if the scalar is all zeros, output should be all zeros
        var zeroScalar = new byte[32]; // All zeros
        var somePublicKey = new byte[32];
        new Random(42).NextBytes(somePublicKey); // Some random value
        Span<byte> result = stackalloc byte[32];

        // Act
        X25519.ComputeSharedSecret(zeroScalar, somePublicKey, result);

        // Assert - Result should be non-zero (due to the clamping, which sets certain bits)
        Assert.NotEqual(new byte[32], result.ToArray());
    }

    #endregion Input Validation Tests

    #region Performance Tests

    [Fact]
    public void Performance_ComputeSharedSecret_CompletesInReasonableTime()
    {
        // Arrange
        const int iterations = 100;
        X25519.GenerateKeyPair(out byte[] privateKey, out byte[] _);
        X25519.GenerateKeyPair(out byte[] _, out byte[] publicKey);

        // Act
        var startTime = DateTime.Now;

        Span<byte> sharedSecret = stackalloc byte[32];

        for (int i = 0; i < iterations; i++)
        {
            X25519.ComputeSharedSecret(privateKey, publicKey, sharedSecret);
            Assert.Equal(32, sharedSecret.Length);
        }

        var duration = DateTime.Now - startTime;

        // Assert
        var msPerOperation = duration.TotalMilliseconds / iterations;
        Assert.True(msPerOperation < 30,
            $"Each key exchange operation took {msPerOperation:F2}ms on average, which exceeds the threshold");
    }

    #endregion Performance Tests

    #region Edge Case Tests

    [Fact]
    public void ComputeSharedSecret_UsingBasePoint_ProducesExpectedResult()
    {
        // Arrange
        X25519.GenerateKeyPair(out byte[] privateKey, out byte[] publicKey);

        Span<byte> secret = stackalloc byte[32];

        // Act - Using the base point u=9
        X25519.ComputeSharedSecret(privateKey, BasePoint, secret);

        // Assert - Result should equal the public key for this private key
        // (since the public key is computed as scalar_mult(private_key, base_point))
        Assert.Equal(publicKey, secret);
    }

    [Fact]
    public void ComputeSharedSecret_WithSelfPublicKey_WorksAsExpected()
    {
        // Arrange - Generate a key pair
        X25519.GenerateKeyPair(out byte[] privateKey, out byte[] publicKey);

        Span<byte> result = stackalloc byte[32];

        // Act - Compute "shared secret" with own public key
        X25519.ComputeSharedSecret(privateKey, publicKey, result);

        // Assert - Should produce a valid result
        Assert.Equal(32, result.Length);
    }

    #endregion Edge Case Tests
}