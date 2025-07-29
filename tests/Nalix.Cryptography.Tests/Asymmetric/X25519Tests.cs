using Nalix.Cryptography.Asymmetric;
using Nalix.Framework.Randomization;
using System;
using Xunit;

namespace Nalix.Cryptography.Tests.Asymmetric;

/// <summary>
/// Test suite for the X25519 elliptic curve Diffie-Hellman implementation.
/// </summary>
public class X25519Tests
{
    [Fact]
    public void GenerateKeyPair_ShouldReturnValidKeyPair()
    {
        var keyPair = X25519.GenerateKeyPair();

        Assert.NotNull(keyPair.PrivateKey);
        Assert.NotNull(keyPair.PublicKey);
        Assert.Equal(32, keyPair.PrivateKey.Length);
        Assert.Equal(32, keyPair.PublicKey.Length);

        // Đảm bảo private key đã được clamp đúng theo RFC
        Assert.Equal(0, keyPair.PrivateKey[0] & 7); // 3 bit thấp của byte đầu là 0
        Assert.Equal(0, keyPair.PrivateKey[31] & 0x80); // bit cao nhất là 0
        Assert.Equal(0x40, keyPair.PrivateKey[31] & 0x40); // bit 6 là 1
    }

    [Fact]
    public void GenerateKeyFromPrivateKey_ShouldReturnCorrectPair()
    {
        Byte[] privateKey = new Byte[32];
        SecureRandom.Fill(privateKey);

        var keyPair = X25519.GenerateKeyFromPrivateKey(privateKey);

        Assert.Same(privateKey, keyPair.PrivateKey);
        Assert.NotNull(keyPair.PublicKey);
        Assert.Equal(32, keyPair.PublicKey.Length);
    }

    [Fact]
    public void Agreement_ShouldReturnSameSecretForBothSides()
    {
        var alice = X25519.GenerateKeyPair();
        var bob = X25519.GenerateKeyPair();

        var aliceSecret = X25519.Agreement(alice.PrivateKey, bob.PublicKey);
        var bobSecret = X25519.Agreement(bob.PrivateKey, alice.PublicKey);

        // 2 phía phải tính ra cùng một shared secret
        Assert.Equal(aliceSecret, bobSecret);
    }
}