using System;
using Nalix.Framework.Security.Asymmetric;
using Xunit;

namespace Nalix.Framework.Tests.Cryptography;

public sealed class X25519Tests
{
    [Fact]
    public void Agreement_WhenPeerKeyLengthIsInvalid_ThrowsArgumentOutOfRangeException()
    {
        byte[] privateKey = new byte[X25519.KeySize];
        byte[] invalidPeerKey = new byte[X25519.KeySize - 1];

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => X25519.Agreement(privateKey, invalidPeerKey));
    }

    [Fact]
    public void GenerateKeyFromPrivateKey_WhenLengthIsInvalid_ThrowsArgumentOutOfRangeException()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => X25519.GenerateKeyFromPrivateKey(new byte[X25519.KeySize - 1]));
    }
}
