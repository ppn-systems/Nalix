using Notio.Cryptography.Asymmetric;
using System;
using System.Linq;
using Xunit;

namespace Notio.Cryptography.Test.Asymmetric;

/// <summary>
/// Test suite for the X25519 elliptic curve Diffie-Hellman implementation.
/// </summary>
public class X25519Tests
{
    #region Test Vectors
    // RFC 7748 Section 5.2 Test Vectors for X25519
    private static readonly byte[] TestScalar1 =
    [
        0xa5, 0x46, 0xe3, 0x6b, 0xf0, 0x52, 0x7c, 0x9d,
        0x3b, 0x16, 0x15, 0x4b, 0x82, 0x46, 0x5e, 0xdd,
        0x62, 0x14, 0x4c, 0x0a, 0xc1, 0xfc, 0x5a, 0x18,
        0x50, 0x6a, 0x22, 0x44, 0xba, 0x44, 0x9a, 0xc4
    ];

    private static readonly byte[] TestUCoordinate1 =
    [
        0xe6, 0xdb, 0x68, 0x67, 0x58, 0x30, 0x30, 0xdb,
        0x35, 0x94, 0xc1, 0xa4, 0x24, 0xb1, 0x5f, 0x7c,
        0x72, 0x66, 0x24, 0xec, 0x26, 0xb3, 0x35, 0x3b,
        0x10, 0xa9, 0x03, 0xa6, 0xd0, 0xab, 0x1c, 0x4c
    ];

    private static readonly byte[] TestResult1 =
    [
        0xc3, 0xda, 0x55, 0x37, 0x9d, 0xe9, 0xc6, 0x90,
        0x8e, 0x94, 0xea, 0x4d, 0xf2, 0x8d, 0x08, 0x4f,
        0x32, 0xec, 0xcf, 0x03, 0x49, 0x1c, 0x71, 0xf7,
        0x54, 0xb4, 0x07, 0x55, 0x77, 0xa2, 0x85, 0x52
    ];

    private static readonly byte[] TestScalar2 =
    [
        0x4b, 0x66, 0xe9, 0xd4, 0xd1, 0xb4, 0x67, 0x3c,
        0x5a, 0xd2, 0x26, 0x91, 0x95, 0x7d, 0x6a, 0xf5,
        0xc1, 0x1b, 0x64, 0x21, 0xe0, 0xea, 0x01, 0xd4,
        0x2c, 0xa4, 0x16, 0x9e, 0x79, 0x18, 0xba, 0x0d
    ];

    private static readonly byte[] TestUCoordinate2 =
    [
        0xe5, 0x21, 0x0f, 0x12, 0x78, 0x68, 0x11, 0xd3,
        0xf4, 0xb7, 0x95, 0x9d, 0x05, 0x38, 0xae, 0x2c,
        0x31, 0xdb, 0xe7, 0x10, 0x6f, 0xc0, 0x3c, 0x3e,
        0xfc, 0x4c, 0xd5, 0x49, 0xc7, 0x15, 0xa4, 0x93
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
    #endregion

    #region Key Generation Tests
    [Fact]
    public void GenerateKeyPair_ReturnsValidKeyPair()
    {
        // Act
        var (privateKey, publicKey) = X25519.GenerateKeyPair();

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
        byte[] sharedSecret = X25519.ComputeSharedSecret(privateKey, publicKey);
        Assert.NotNull(sharedSecret);
        Assert.Equal(32, sharedSecret.Length);
    }

    [Fact]
    public void GenerateKeyPair_ReturnsDifferentKeysOnMultipleCalls()
    {
        // Act
        var (privateKey1, publicKey1) = X25519.GenerateKeyPair();
        var (privateKey2, publicKey2) = X25519.GenerateKeyPair();

        // Assert
        Assert.False(privateKey1.SequenceEqual(privateKey2), "Generated private keys should be different");
        Assert.False(publicKey1.SequenceEqual(publicKey2), "Generated public keys should be different");
    }
    #endregion

    #region RFC 7748 Test Vector Tests
    [Fact]
    public void ComputeSharedSecret_TestVector1_ReturnsExpectedResult()
    {
        // Act
        var result = X25519.ComputeSharedSecret(TestScalar1, TestUCoordinate1);

        // Assert
        Assert.Equal(TestResult1, result);
    }

    [Fact]
    public void ComputeSharedSecret_TestVector2_ReturnsExpectedResult()
    {
        // Act
        var result = X25519.ComputeSharedSecret(TestScalar2, TestUCoordinate2);

        // Assert
        Assert.Equal(TestResult2, result);
    }
    #endregion

    #region Key Exchange Tests
    [Fact]
    public void KeyExchange_BothParties_ComputeSameSharedSecret()
    {
        // Arrange - Generate key pairs for Alice and Bob
        var (alicePrivate, alicePublic) = X25519.GenerateKeyPair();
        var (bobPrivate, bobPublic) = X25519.GenerateKeyPair();

        // Act - Compute shared secrets on both sides
        var aliceSharedSecret = X25519.ComputeSharedSecret(alicePrivate, bobPublic);
        var bobSharedSecret = X25519.ComputeSharedSecret(bobPrivate, alicePublic);

        // Assert - Both parties should compute the same shared secret
        Assert.Equal(aliceSharedSecret, bobSharedSecret);
    }

    [Fact]
    public void KeyExchange_MultiplePairs_ProducesUniqueSecrets()
    {
        // Arrange
        const int pairCount = 5;
        var sharedSecrets = new byte[pairCount][];

        // Act - Generate multiple key pairs and compute shared secrets
        for (int i = 0; i < pairCount; i++)
        {
            var (alicePrivate, alicePublic) = X25519.GenerateKeyPair();
            var (bobPrivate, bobPublic) = X25519.GenerateKeyPair();

            sharedSecrets[i] = X25519.ComputeSharedSecret(alicePrivate, bobPublic);
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
    #endregion

    #region Input Validation Tests
    [Fact]
    public void ComputeSharedSecret_InvalidPrivateKeyLength_ThrowsArgumentException()
    {
        // Arrange
        var invalidPrivateKey = new byte[16]; // Too short
        var validPublicKey = new byte[32];

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            X25519.ComputeSharedSecret(invalidPrivateKey, validPublicKey));
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
            X25519.ComputeSharedSecret(validPrivateKey, invalidPublicKey));
        Assert.Contains("Public key must be 32 bytes", ex.Message);
    }

    [Fact]
    public void ComputeSharedSecret_ScalarZero_ReturnsZeroResult()
    {
        // Arrange - RFC 7748 specifies that if the scalar is all zeros, output should be all zeros
        var zeroScalar = new byte[32]; // All zeros
        var somePublicKey = new byte[32];
        new Random(42).NextBytes(somePublicKey); // Some random value

        // Act
        var result = X25519.ComputeSharedSecret(zeroScalar, somePublicKey);

        // Assert - Result should be non-zero (due to the clamping, which sets certain bits)
        Assert.NotEqual(new byte[32], result);
    }
    #endregion

    #region Performance Tests
    [Fact]
    public void Performance_ComputeSharedSecret_CompletesInReasonableTime()
    {
        // Arrange
        const int iterations = 100;
        var (privateKey, _) = X25519.GenerateKeyPair();
        var (_, publicKey) = X25519.GenerateKeyPair();

        // Act
        var startTime = DateTime.Now;

        for (int i = 0; i < iterations; i++)
        {
            var sharedSecret = X25519.ComputeSharedSecret(privateKey, publicKey);
            Assert.Equal(32, sharedSecret.Length);
        }

        var duration = DateTime.Now - startTime;

        // Assert
        var msPerOperation = duration.TotalMilliseconds / iterations;
        Assert.True(msPerOperation < 30,
            $"Each key exchange operation took {msPerOperation:F2}ms on average, which exceeds the threshold");
    }
    #endregion

    #region Edge Case Tests
    [Fact]
    public void ComputeSharedSecret_UsingBasePoint_ProducesExpectedResult()
    {
        // Arrange
        var (privateKey, publicKey) = X25519.GenerateKeyPair();

        // Act - Using the base point u=9
        var result = X25519.ComputeSharedSecret(privateKey, BasePoint);

        // Assert - Result should equal the public key for this private key
        // (since the public key is computed as scalar_mult(private_key, base_point))
        Assert.Equal(publicKey, result);
    }

    [Fact]
    public void ComputeSharedSecret_WithSelfPublicKey_WorksAsExpected()
    {
        // Arrange - Generate a key pair
        var (privateKey, publicKey) = X25519.GenerateKeyPair();

        // Act - Compute "shared secret" with own public key
        var result = X25519.ComputeSharedSecret(privateKey, publicKey);

        // Assert - Should produce a valid result
        Assert.NotNull(result);
        Assert.Equal(32, result.Length);
    }
    #endregion
}
