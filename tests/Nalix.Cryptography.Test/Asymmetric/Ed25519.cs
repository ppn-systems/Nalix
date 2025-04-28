using Nalix.Cryptography.Asymmetric;
using System;
using System.Text;
using Xunit;

namespace Nalix.Cryptography.Test.Asymmetric;

public class Ed25519Tests
{
    [Fact]
    public void Verify_InvalidSignature_ShouldFail()
    {
        // Arrange
        byte[] privateKey = new byte[32];
        Random.Shared.NextBytes(privateKey);
        byte[] message = Encoding.UTF8.GetBytes("Secure message");
        var signature = Ed25519.Sign(message, privateKey);

        // Tamper the signature
        signature[0] ^= 0xFF;
        _ = Ed25519.Sign(message, privateKey); // re-sign
        byte[] publicKey = new byte[32];
        Random.Shared.NextBytes(publicKey); // invalid random publicKey

        // Act & Assert
        Assert.False(Ed25519.Verify(signature, message, publicKey));
    }

    [Fact]
    public void Sign_NullMessage_ShouldThrow()
    {
        // Arrange
        byte[] privateKey = new byte[32];
        Random.Shared.NextBytes(privateKey);

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => Ed25519.Sign(null!, privateKey));
        Assert.Contains("Message cannot be null", ex.Message);
    }

    [Fact]
    public void Sign_InvalidPrivateKeyLength_ShouldThrow()
    {
        // Arrange
        byte[] privateKey = new byte[16]; // Wrong size
        byte[] message = Encoding.UTF8.GetBytes("Test");

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => Ed25519.Sign(message, privateKey));
        Assert.Contains("Private key must be 32 bytes", ex.Message);
    }

    [Fact]
    public void Verify_InvalidSignatureLength_ShouldThrow()
    {
        // Arrange
        byte[] signature = new byte[30]; // Invalid length
        byte[] message = Encoding.UTF8.GetBytes("Test");
        byte[] publicKey = new byte[32];
        Random.Shared.NextBytes(publicKey);

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => Ed25519.Verify(signature, message, publicKey));
        Assert.Contains("Signature must be", ex.Message);
    }

    [Fact]
    public void Verify_InvalidPublicKeyLength_ShouldThrow()
    {
        // Arrange
        byte[] signature = new byte[Ed25519.SignatureSize];
        byte[] message = Encoding.UTF8.GetBytes("Test");
        byte[] publicKey = new byte[16]; // Wrong size

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => Ed25519.Verify(signature, message, publicKey));
        Assert.Contains("Public key must be", ex.Message);
    }
}
