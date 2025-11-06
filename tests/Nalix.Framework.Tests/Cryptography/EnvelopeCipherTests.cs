using Nalix.Common.Enums;
using Nalix.Framework.Cryptography;
using System;
using System.Linq;
using System.Security.Cryptography;
using Xunit;

namespace Nalix.Framework.Tests.Cryptography;

/// <summary>
/// xUnit tests cho EnvelopeCipher.
/// Lưu ý: các test giả định rằng các engine (AeadEngine/SymmetricEngine)
/// đã được triển khai trong cùng repo và hoạt động với các key length tiêu chuẩn.
/// Nếu một suite yêu cầu kích thước key khác, hãy điều chỉnh RandomKeyLength tương ứng.
/// </summary>
public class EnvelopeCipherTests
{
    private static Byte[] RandomBytes(Int32 len)
    {
        var b = new Byte[len];
        RandomNumberGenerator.Fill(b);
        return b;
    }

    [Fact(DisplayName = "AEAD ChaCha20-Poly1305: encrypt -> decrypt roundtrip")]
    public void EncryptDecrypt_AEAD_ChaCha20Poly1305_RoundTrip()
    {
        // ChaCha20-Poly1305 thường dùng key 32 bytes
        var key = RandomBytes(32);
        var plaintext = RandomBytes(256);
        var aad = RandomBytes(16);

        var envelope = EnvelopeCipher.Encrypt(key, plaintext, CipherSuiteType.ChaCha20Poly1305, aad);

        Assert.NotNull(envelope);
        Assert.True(EnvelopeCipher.Decrypt(key, envelope, out var decrypted, aad));
        Assert.NotNull(decrypted);
        Assert.True(plaintext.SequenceEqual(decrypted));
    }

    [Fact(DisplayName = "Symmetric ChaCha20: encrypt -> decrypt roundtrip")]
    public void EncryptDecrypt_Symmetric_ChaCha20_RoundTrip()
    {
        // ChaCha20 stream key 32 bytes
        var key = RandomBytes(32);
        var plaintext = RandomBytes(128);

        var envelope = EnvelopeCipher.Encrypt(key, plaintext, CipherSuiteType.ChaCha20);

        Assert.NotNull(envelope);
        // SymmetricEngine.Decrypt signature in EnvelopeCipher ignores aad param for non-AEAD
        Assert.True(EnvelopeCipher.Decrypt(key, envelope, out var decrypted));
        Assert.NotNull(decrypted);
        Assert.True(plaintext.SequenceEqual(decrypted));
    }

    [Fact(DisplayName = "AEAD decrypt with wrong key fails authentication")]
    public void Decrypt_AEAD_WrongKey_Fails()
    {
        var key = RandomBytes(32);
        var plaintext = RandomBytes(64);
        var aad = RandomBytes(8);

        var envelope = EnvelopeCipher.Encrypt(key, plaintext, CipherSuiteType.ChaCha20Poly1305, aad);

        var wrongKey = RandomBytes(32);
        var ok = EnvelopeCipher.Decrypt(wrongKey, envelope, out var decrypted, aad);

        // AEAD should fail authentication and return false
        Assert.False(ok);
        Assert.Null(decrypted);
    }

    [Fact(DisplayName = "AEAD decrypt with wrong AAD fails authentication")]
    public void Decrypt_AEAD_WrongAad_Fails()
    {
        var key = RandomBytes(32);
        var plaintext = RandomBytes(64);
        var aad = RandomBytes(12);

        var envelope = EnvelopeCipher.Encrypt(key, plaintext, CipherSuiteType.ChaCha20Poly1305, aad);

        var wrongAad = RandomBytes(12);
        var ok = EnvelopeCipher.Decrypt(key, envelope, out var decrypted, wrongAad);

        Assert.False(ok);
        Assert.Null(decrypted);
    }

    [Fact(DisplayName = "Decrypt invalid envelope returns false")]
    public void Decrypt_InvalidEnvelope_ReturnsFalse()
    {
        var key = RandomBytes(32);
        var garbage = RandomBytes(5); // too short / malformed

        var ok = EnvelopeCipher.Decrypt(key, garbage, out var decrypted);

        Assert.False(ok);
        Assert.Null(decrypted);
    }

    [Fact(DisplayName = "Empty plaintext roundtrip works for AEAD")]
    public void EncryptDecrypt_AEAD_EmptyPlaintext_RoundTrip()
    {
        var key = RandomBytes(32);
        var plaintext = Array.Empty<Byte>();
        var aad = RandomBytes(4);

        var envelope = EnvelopeCipher.Encrypt(key, plaintext, CipherSuiteType.ChaCha20Poly1305, aad);

        Assert.NotNull(envelope);
        Assert.True(EnvelopeCipher.Decrypt(key, envelope, out var decrypted, aad));
        Assert.NotNull(decrypted);
        Assert.True(decrypted.Length == 0);
    }
}