using Nalix.Common.Enums;
using Nalix.Shared.Security.Engine;
using System;
using Xunit;

namespace Nalix.Shared.Tests.Cryptography;

public class AeadEngineTests
{
    [Fact]
    public void EncryptDecrypt_ChaCha20Poly1305_Roundtrip_Succeeds()
    {
        Byte[] key = new Byte[32];
        for (Int32 i = 0; i < key.Length; i++)
        {
            key[i] = (Byte)(i + 1);
        }

        Byte[] plaintext = System.Text.Encoding.UTF8.GetBytes("Test plaintext for AEAD engine");
        Byte[] aad = System.Text.Encoding.UTF8.GetBytes("header-aad");

        // Use fixed seq to make test deterministic
        Byte[] envelope = AeadEngine.Encrypt(key, plaintext, CipherSuiteType.CHACHA20_POLY1305, aad, seq: 0x12345678u);

        Assert.NotNull(envelope);
        Assert.True(envelope.Length > plaintext.Length);

        Boolean ok = AeadEngine.Decrypt(key, envelope, out var decrypted, aad);
        Assert.True(ok);
        Assert.NotNull(decrypted);
        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void Encrypt_InvalidKeyLength_ThrowsArgumentException()
    {
        var badKey = new Byte[16]; // too short for ChaCha20-Poly1305
        var plaintext = new Byte[10];

        var ex = Assert.Throws<ArgumentException>(() =>
            AeadEngine.Encrypt(badKey, plaintext, CipherSuiteType.CHACHA20_POLY1305));
        Assert.Equal("key", ex.ParamName);
    }

    [Fact]
    public void Decrypt_TamperedEnvelope_FailsVerification()
    {
        var key = new Byte[32];
        for (Int32 i = 0; i < key.Length; i++)
        {
            key[i] = (Byte)(i + 2);
        }

        var plaintext = System.Text.Encoding.UTF8.GetBytes("Sensitive message");
        var envelope = AeadEngine.Encrypt(key, plaintext, CipherSuiteType.CHACHA20_POLY1305, seq: 1);

        // Tamper: flip the last byte (tag area is at the end)
        envelope[^1] ^= 0xFF;

        var ok = AeadEngine.Decrypt(key, envelope, out var decrypted);
        Assert.False(ok);
        Assert.Null(decrypted);
    }
}