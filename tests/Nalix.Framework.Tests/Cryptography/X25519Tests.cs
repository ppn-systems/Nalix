using System;
using Nalix.Abstractions.Primitives;
using Nalix.Codec.Security.Asymmetric;
using Nalix.Codec.Security.Primitives;
using Xunit;

namespace Nalix.Framework.Tests.Cryptography;

public sealed class X25519Tests
{
    [Fact]
    public void X25519_RoundTripAgreement_ProducesSameSharedSecret()
    {
        // 1. Generate two key pairs
        X25519.X25519KeyPair alice = X25519.GenerateKeyPair();
        X25519.X25519KeyPair bob = X25519.GenerateKeyPair();

        // 2. Perform agreement
        Bytes32 aliceShared = X25519.Agreement(alice.PrivateKey, bob.PublicKey);
        Bytes32 bobShared = X25519.Agreement(bob.PrivateKey, alice.PublicKey);

        // 3. Verify
        Assert.False(aliceShared.IsZero);
        Assert.False(bobShared.IsZero);
        Assert.Equal(aliceShared, bobShared);
    }

    [Fact]
    public void GenerateKeyFromPrivateKey_ProducesSamePublicKey()
    {
        X25519.X25519KeyPair original = X25519.GenerateKeyPair();
        X25519.X25519KeyPair derived = X25519.GenerateKeyFromPrivateKey(original.PrivateKey);

        Assert.Equal(original.PublicKey, derived.PublicKey);
    }
}













