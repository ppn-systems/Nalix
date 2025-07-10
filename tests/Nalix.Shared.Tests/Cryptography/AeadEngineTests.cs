using Nalix.Common.Enums;
using Nalix.Shared.Security.Engine;
using System;
using Xunit;

namespace Nalix.Shared.Tests.Cryptography;

public class AeadEngineTests
{
    [Fact]
    public void ConvertKeyToXtea_Valid32Bytes_ProducesXorOfHalves()
    {
        var key32 = new Byte[32];
        for (Int32 i = 0; i < 32; i++)
        {
            key32[i] = (Byte)i;
        }

        var out16 = new Byte[16];
        AeadEngine.U32ToU16(key32, out16);

        for (Int32 i = 0; i < 16; i++)
        {
            Assert.Equal((Byte)(key32[i] ^ key32[i + 16]), out16[i]);
        }
    }

    [Fact]
    public void ConvertKeyToXtea_InvalidKeyLength_ThrowsArgumentException()
    {
        var badKey = new Byte[31];
        var out16 = new Byte[16];

        var ex = Assert.Throws<ArgumentException>(() => AeadEngine.U32ToU16(badKey, out16));
        Assert.Equal("key", ex.ParamName);
    }

    [Fact]
    public void ConvertKeyToXtea_OutTooSmall_ThrowsArgumentException()
    {
        var key32 = new Byte[32];
        var outTooSmall = new Byte[8];

        var ex = Assert.Throws<ArgumentException>(() => AeadEngine.U32ToU16(key32, outTooSmall));
        Assert.Equal("out16", ex.ParamName);
    }

    [Fact]
    public void EncryptDecrypt_ChaCha20Poly1305_Roundtrip_Succeeds()
    {
        var key = new Byte[32];
        for (Int32 i = 0; i < key.Length; i++)
        {
            key[i] = (Byte)(i + 1);
        }

        var plaintext = System.Text.Encoding.UTF8.GetBytes("Test plaintext for AEAD engine");
        var aad = System.Text.Encoding.UTF8.GetBytes("header-aad");

        // Use fixed seq to make test deterministic
        var envelope = AeadEngine.Encrypt(key, plaintext, CipherSuiteType.CHACHA20_POLY1305, aad, seq: 0x12345678u);

        Assert.NotNull(envelope);
        Assert.True(envelope.Length > plaintext.Length);

        var ok = AeadEngine.Decrypt(key, envelope, out var decrypted, aad);
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