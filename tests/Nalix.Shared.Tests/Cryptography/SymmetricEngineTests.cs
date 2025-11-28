using Nalix.Common.Enums;
using Nalix.Shared.Security.Engine;
using Nalix.Shared.Security.Symmetric;
using System;
using System.Text;
using Xunit;

namespace Nalix.Shared.Tests.Cryptography;

public class SymmetricEngineTests
{
    [Fact]
    public void ConvertKeyToXtea_Valid32Bytes_ProducesXorOfHalves()
    {
        var key32 = new Byte[32];
        for (Int32 i = 0; i < 32; i++)
        {
            key32[i] = (Byte)(0xFF - i);
        }

        var out16 = new Byte[16];
        SymmetricEngine.ConvertKeyToXtea(key32, out16);

        for (Int32 i = 0; i < 16; i++)
        {
            Assert.Equal((Byte)(key32[i] ^ key32[i + 16]), out16[i]);
        }
    }

    [Fact]
    public void ConvertKeyToXtea_InvalidKeyLength_ThrowsArgumentException()
    {
        var badKey = Array.Empty<Byte>();
        var out16 = new Byte[16];

        var ex = Assert.Throws<ArgumentException>(() => SymmetricEngine.ConvertKeyToXtea(badKey, out16));
        Assert.Equal("key", ex.ParamName);
    }

    [Fact]
    public void ConvertKeyToXtea_OutTooSmall_ThrowsArgumentException()
    {
        var key32 = new Byte[32];
        var outTooSmall = new Byte[4];

        var ex = Assert.Throws<ArgumentException>(() => SymmetricEngine.ConvertKeyToXtea(key32, outTooSmall));
        Assert.Equal("out16", ex.ParamName);
    }

    [Fact]
    public void EncryptDecrypt_Envelope_Roundtrip_ChaCha20_Succeeds()
    {
        var key = new Byte[ChaCha20.KeySize];
        for (Int32 i = 0; i < key.Length; i++)
        {
            key[i] = (Byte)(i + 5);
        }

        var plaintext = Encoding.UTF8.GetBytes("Symmetric envelope payload");
        var nonce = new Byte[ChaCha20.NonceSize];
        for (Int32 i = 0; i < nonce.Length; i++)
        {
            nonce[i] = (Byte)i;
        }

        // Use fixed seq to make counter deterministic
        var envelope = SymmetricEngine.Encrypt(key, plaintext, CipherSuiteType.CHACHA20, nonce, seq: 42);

        Assert.NotNull(envelope);

        var success = SymmetricEngine.Decrypt(key, envelope, out var decrypted);
        Assert.True(success);
        Assert.NotNull(decrypted);
        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void Encrypt_WithCounter_DecryptionXorMatches()
    {
        var key = new Byte[ChaCha20.KeySize];
        for (Int32 i = 0; i < key.Length; i++)
        {
            key[i] = (Byte)((i * 3) + 1);
        }

        var nonce = new Byte[ChaCha20.NonceSize];
        for (Int32 i = 0; i < nonce.Length; i++)
        {
            nonce[i] = (Byte)(i + 10);
        }

        var src = Encoding.UTF8.GetBytes("Stream XOR test data for symmetric engine");

        // Use the span-first API that returns a new buffer
        var ct = SymmetricEngine.Encrypt(CipherSuiteType.CHACHA20, key, nonce, counter: 7, src);

        // Decrypt by XOR-ing again with same params (idempotent)
        var pt = SymmetricEngine.Decrypt(CipherSuiteType.CHACHA20, key, nonce, counter: 7, ct);

        Assert.Equal(src, pt);
    }

    [Fact]
    public void Encrypt_InvalidNonceLength_ThrowsArgumentException()
    {
        var key = new Byte[ChaCha20.KeySize];
        var plaintext = new Byte[8];
        var badNonce = new Byte[4]; // wrong length

        var ex = Assert.Throws<ArgumentException>(() =>
            SymmetricEngine.Encrypt(key, plaintext, CipherSuiteType.CHACHA20, badNonce));
        Assert.Equal("nonce", ex.ParamName);
    }
}