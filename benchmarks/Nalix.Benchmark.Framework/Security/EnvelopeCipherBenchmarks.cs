using System;
using BenchmarkDotNet.Attributes;
using Nalix.Benchmark.Framework.Abstractions;
using Nalix.Common.Security;
using Nalix.Framework.Security;

namespace Nalix.Benchmark.Framework.Security;

/// <summary>
/// Benchmarks for high-level EnvelopeCipher performance across various cipher suites.
/// </summary>
public class EnvelopeCipherBenchmarks : NalixBenchmarkBase
{
    private const int EnvelopeOverhead = 128;

    private byte[] _key = null!;
    private byte[] _aad = null!;
    private byte[] _plaintext64 = null!;
    private byte[] _plaintext1024 = null!;
    private byte[] _ciphertext64 = null!;
    private byte[] _ciphertext1024 = null!;
    private byte[] _envelope64_chacha20 = null!;
    private byte[] _envelope64_chacha20poly1305 = null!;
    private byte[] _envelope64_salsa20 = null!;
    private byte[] _envelope64_salsa20poly1305 = null!;
    private byte[] _envelope1024_chacha20 = null!;
    private byte[] _envelope1024_chacha20poly1305 = null!;
    private byte[] _envelope1024_salsa20 = null!;
    private byte[] _envelope1024_salsa20poly1305 = null!;
    private byte[] _decryptOut = null!;

    [Params(64, 1024)]
    public int PayloadBytes { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _key = new byte[32];
        _aad = new byte[16];
        _plaintext64 = new byte[64];
        _plaintext1024 = new byte[1024];
        _ciphertext64 = new byte[64 + EnvelopeOverhead];
        _ciphertext1024 = new byte[1024 + EnvelopeOverhead];
        _decryptOut = new byte[1024 + EnvelopeOverhead];

        System.Random.Shared.NextBytes(_key);
        System.Random.Shared.NextBytes(_aad);
        System.Random.Shared.NextBytes(_plaintext64);
        System.Random.Shared.NextBytes(_plaintext1024);

        _envelope64_chacha20 = EncryptHelper(64, CipherSuiteType.Chacha20, false);
        _envelope64_chacha20poly1305 = EncryptHelper(64, CipherSuiteType.Chacha20Poly1305, true);
        _envelope64_salsa20 = EncryptHelper(64, CipherSuiteType.Salsa20, false);
        _envelope64_salsa20poly1305 = EncryptHelper(64, CipherSuiteType.Salsa20Poly1305, true);

        _envelope1024_chacha20 = EncryptHelper(1024, CipherSuiteType.Chacha20, false);
        _envelope1024_chacha20poly1305 = EncryptHelper(1024, CipherSuiteType.Chacha20Poly1305, true);
        _envelope1024_salsa20 = EncryptHelper(1024, CipherSuiteType.Salsa20, false);
        _envelope1024_salsa20poly1305 = EncryptHelper(1024, CipherSuiteType.Salsa20Poly1305, true);
    }

    private byte[] EncryptHelper(int size, CipherSuiteType suite, bool withAad)
    {
        byte[] buf = new byte[size + EnvelopeOverhead];
        ReadOnlySpan<byte> pt = size == 64 ? _plaintext64 : _plaintext1024;
        EnvelopeCipher.Encrypt(_key, pt, buf, withAad ? _aad : default, null, suite, out int written);
        return buf[..written];
    }

    private ReadOnlySpan<byte> Plaintext => PayloadBytes == 64 ? _plaintext64 : _plaintext1024;
    private Span<byte> CiphertextBuf => PayloadBytes == 64 ? _ciphertext64 : _ciphertext1024;

    private ReadOnlySpan<byte> GetEnvelope(CipherSuiteType suite) => suite switch
    {
        CipherSuiteType.Chacha20 => PayloadBytes == 64 ? _envelope64_chacha20 : _envelope1024_chacha20,
        CipherSuiteType.Chacha20Poly1305 => PayloadBytes == 64 ? _envelope64_chacha20poly1305 : _envelope1024_chacha20poly1305,
        CipherSuiteType.Salsa20 => PayloadBytes == 64 ? _envelope64_salsa20 : _envelope1024_salsa20,
        CipherSuiteType.Salsa20Poly1305 => PayloadBytes == 64 ? _envelope64_salsa20poly1305 : _envelope1024_salsa20poly1305,
        _ => throw new ArgumentOutOfRangeException(nameof(suite))
    };

    [BenchmarkCategory("Encrypt"), Benchmark(Description = "Encrypt (ChaCha20)")]
    public int EncryptChaCha20() { EnvelopeCipher.Encrypt(_key, Plaintext, CiphertextBuf, null, CipherSuiteType.Chacha20, out int written); return written; }

    [BenchmarkCategory("Encrypt"), Benchmark(Description = "Encrypt (ChaCha20-Poly1305)")]
    public int EncryptChaCha20Poly1305() { EnvelopeCipher.Encrypt(_key, Plaintext, CiphertextBuf, _aad, null, CipherSuiteType.Chacha20Poly1305, out int written); return written; }

    [BenchmarkCategory("Encrypt"), Benchmark(Description = "Encrypt (Salsa20)")]
    public int EncryptSalsa20() { EnvelopeCipher.Encrypt(_key, Plaintext, CiphertextBuf, null, CipherSuiteType.Salsa20, out int written); return written; }

    [BenchmarkCategory("Encrypt"), Benchmark(Description = "Encrypt (Salsa20-Poly1305)")]
    public int EncryptSalsa20Poly1305() { EnvelopeCipher.Encrypt(_key, Plaintext, CiphertextBuf, _aad, null, CipherSuiteType.Salsa20Poly1305, out int written); return written; }

    [BenchmarkCategory("Decrypt"), Benchmark(Description = "Decrypt (ChaCha20)")]
    public int DecryptChaCha20() { EnvelopeCipher.Decrypt(_key, GetEnvelope(CipherSuiteType.Chacha20), _decryptOut, CipherSuiteType.Chacha20, out int written); return written; }

    [BenchmarkCategory("Decrypt"), Benchmark(Description = "Decrypt (ChaCha20-Poly1305)")]
    public int DecryptChaCha20Poly1305() { EnvelopeCipher.Decrypt(_key, GetEnvelope(CipherSuiteType.Chacha20Poly1305), _decryptOut, _aad, CipherSuiteType.Chacha20Poly1305, out int written); return written; }

    [BenchmarkCategory("Decrypt"), Benchmark(Description = "Decrypt (Salsa20)")]
    public int DecryptSalsa20() { EnvelopeCipher.Decrypt(_key, GetEnvelope(CipherSuiteType.Salsa20), _decryptOut, CipherSuiteType.Salsa20, out int written); return written; }

    [BenchmarkCategory("Decrypt"), Benchmark(Description = "Decrypt (Salsa20-Poly1305)")]
    public int DecryptSalsa20Poly1305() { EnvelopeCipher.Decrypt(_key, GetEnvelope(CipherSuiteType.Salsa20Poly1305), _decryptOut, _aad, CipherSuiteType.Salsa20Poly1305, out int written); return written; }
}
