// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Codec.Security.Hashing;
using Xunit;

namespace Nalix.Framework.Tests.Cryptography;

public sealed class Pbkdf2Tests
{
    [Fact]
    public void HashAndVerifyWithSameCredentialReturnsTrue()
    {
        const string credential = "nalix-secret-123";

        Pbkdf2.Hash(credential, out byte[] salt, out byte[] hash);
        bool ok = Pbkdf2.Verify(credential, salt, hash);

        Assert.Equal(Pbkdf2.SaltSize, salt.Length);
        Assert.Equal(Pbkdf2.KeySize, hash.Length);
        Assert.True(ok);
    }

    [Fact]
    public void VerifyWithWrongCredentialReturnsFalse()
    {
        const string credential = "correct-password";

        Pbkdf2.Hash(credential, out byte[] salt, out byte[] hash);
        bool ok = Pbkdf2.Verify("wrong-password", salt, hash);

        Assert.False(ok);
    }

    [Fact]
    public void EncodedHashAndVerifyRoundTripWorks()
    {
        const string credential = "credential-v2";

        string encoded = Pbkdf2.Encoded.Hash(credential);
        bool ok = Pbkdf2.Encoded.Verify(credential, encoded);
        bool bad = Pbkdf2.Encoded.Verify("bad-credential", encoded);

        Assert.False(string.IsNullOrWhiteSpace(encoded));
        Assert.True(ok);
        Assert.False(bad);
    }

    [Fact]
    public void EncodedVerifyWhenInputMalformedOrVersionChangedReturnsFalse()
    {
        const string credential = "credential-v2";
        string encoded = Pbkdf2.Encoded.Hash(credential);

        Assert.False(Pbkdf2.Encoded.Verify(credential, "not-base64"));
        Assert.False(Pbkdf2.Encoded.Verify(credential, Convert.ToBase64String([1, 2, 3])));

        byte[] blob = Convert.FromBase64String(encoded);
        blob[0] ^= 0x01; // change version byte
        string changedVersion = Convert.ToBase64String(blob);

        Assert.False(Pbkdf2.Encoded.Verify(credential, changedVersion));
    }
}













