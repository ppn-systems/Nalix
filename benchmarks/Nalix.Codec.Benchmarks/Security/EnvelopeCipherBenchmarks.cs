using System;
using BenchmarkDotNet.Attributes;
using Nalix.Abstractions.Security;
using Nalix.Benchmarks.Shared;
using Nalix.Codec.Security;

namespace Nalix.Codec.Benchmarks.Security;

[Config(typeof(NalixBenchmarkConfig))]
public class EnvelopeCipherBenchmarks
{
    private static readonly byte[] s_key = new byte[32];
    private static readonly byte[] s_aad = "nalix-associated-data-payload-security-integrity"u8.ToArray();

    [Params(64, 1024)]
    public int PayloadSize;

    private byte[] _plaintext = null!;
    private byte[] _ciphertext = null!;
    private byte[] _decrypted = null!;

    private byte[] _envelopeSalsa20 = null!;
    private byte[] _envelopeChacha20 = null!;
    private byte[] _envelopeSalsa20Poly = null!;
    private byte[] _envelopeChacha20Poly = null!;

    static EnvelopeCipherBenchmarks()
    {
        Random.Shared.NextBytes(s_key);
    }

    [GlobalSetup]
    public void Setup()
    {
        _plaintext = new byte[PayloadSize];
        Random.Shared.NextBytes(_plaintext);

        // Max envelope size: original + tag (16B) + header/nonce (approx 32B)
        int maxLen = PayloadSize + 128;
        _ciphertext = new byte[maxLen];
        _decrypted = new byte[maxLen];

        // Pre-compute envelopes
        EnvelopeCipher.Encrypt(s_key, _plaintext, _ciphertext, s_aad, 1, CipherSuiteType.Salsa20, out int written);
        _envelopeSalsa20 = _ciphertext[..written];

        EnvelopeCipher.Encrypt(s_key, _plaintext, _ciphertext, s_aad, 1, CipherSuiteType.Chacha20, out written);
        _envelopeChacha20 = _ciphertext[..written];

        EnvelopeCipher.Encrypt(s_key, _plaintext, _ciphertext, s_aad, 1, CipherSuiteType.Salsa20Poly1305, out written);
        _envelopeSalsa20Poly = _ciphertext[..written];

        EnvelopeCipher.Encrypt(s_key, _plaintext, _ciphertext, s_aad, 1, CipherSuiteType.Chacha20Poly1305, out written);
        _envelopeChacha20Poly = _ciphertext[..written];
    }

    // ── Salsa20 ──

    [Benchmark]
    public void Encrypt_Salsa20()
    {
        EnvelopeCipher.Encrypt(s_key, _plaintext, _ciphertext, s_aad, 1, CipherSuiteType.Salsa20, out _);
    }

    [Benchmark]
    public void Decrypt_Salsa20()
    {
        EnvelopeCipher.Decrypt(s_key, _envelopeSalsa20, _decrypted, s_aad, CipherSuiteType.Salsa20, out _, out _);
    }

    // ── Chacha20 ──

    [Benchmark]
    public void Encrypt_Chacha20()
    {
        EnvelopeCipher.Encrypt(s_key, _plaintext, _ciphertext, s_aad, 1, CipherSuiteType.Chacha20, out _);
    }

    [Benchmark]
    public void Decrypt_Chacha20()
    {
        EnvelopeCipher.Decrypt(s_key, _envelopeChacha20, _decrypted, s_aad, CipherSuiteType.Chacha20, out _, out _);
    }

    // ── Salsa20Poly1305 ──

    [Benchmark]
    public void Encrypt_Salsa20Poly1305()
    {
        EnvelopeCipher.Encrypt(s_key, _plaintext, _ciphertext, s_aad, 1, CipherSuiteType.Salsa20Poly1305, out _);
    }

    [Benchmark]
    public void Decrypt_Salsa20Poly1305()
    {
        EnvelopeCipher.Decrypt(s_key, _envelopeSalsa20Poly, _decrypted, s_aad, CipherSuiteType.Salsa20Poly1305, out _, out _);
    }

    // ── Chacha20Poly1305 ──

    [Benchmark]
    public void Encrypt_Chacha20Poly1305()
    {
        EnvelopeCipher.Encrypt(s_key, _plaintext, _ciphertext, s_aad, 1, CipherSuiteType.Chacha20Poly1305, out _);
    }

    [Benchmark]
    public void Decrypt_Chacha20Poly1305()
    {
        EnvelopeCipher.Decrypt(s_key, _envelopeChacha20Poly, _decrypted, s_aad, CipherSuiteType.Chacha20Poly1305, out _, out _);
    }
}
