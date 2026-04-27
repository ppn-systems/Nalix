using System;
using BenchmarkDotNet.Attributes;
using Nalix.Benchmark.Framework.Abstractions;
using Nalix.Abstractions.Security;
using Nalix.Framework.Security.Engine;

namespace Nalix.Benchmark.Framework.Security;

/// <summary>
/// Benchmarks for low-level symmetric and AEAD cipher engines.
/// Compares encryption vs decryption performance for ChaCha20 and ChaCha20-Poly1305.
/// </summary>
public class CipherEngineBenchmarks : NalixBenchmarkBase
{
    private byte[] _key = null!;
    private byte[] _nonce12 = null!;
    private byte[] _aad = null!;
    private byte[] _plaintext = null!;
    private byte[] _symEnvelope = null!;
    private byte[] _aeadEnvelope = null!;
    private byte[] _output = null!;
    private int _symWritten;
    private int _aeadWritten;

    [Params(64, 1024)]
    public int PayloadBytes { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _key = new byte[32];
        _nonce12 = new byte[12];
        _aad = new byte[16];
        _plaintext = new byte[PayloadBytes];
        _output = new byte[PayloadBytes + 128];

        System.Random.Shared.NextBytes(_key);
        System.Random.Shared.NextBytes(_nonce12);
        System.Random.Shared.NextBytes(_aad);
        System.Random.Shared.NextBytes(_plaintext);

        _symEnvelope = new byte[PayloadBytes + 64];
        SymmetricEngine.Encrypt(_key, _plaintext, _symEnvelope, _nonce12, 7u, CipherSuiteType.Chacha20, out _symWritten);

        _aeadEnvelope = new byte[PayloadBytes + 64];
        AeadEngine.Encrypt(_key, _plaintext, _aeadEnvelope, _nonce12, _aad, 7u, CipherSuiteType.Chacha20Poly1305, out _aeadWritten);
    }

    /// <summary>Encrypts payload using symmetric stream cipher (ChaCha20).</summary>
    [BenchmarkCategory("Symmetric"), Benchmark(Baseline = true)]
    public int SymmetricEncrypt()
    {
        SymmetricEngine.Encrypt(_key, _plaintext, _symEnvelope, _nonce12, 7u, CipherSuiteType.Chacha20, out int written);
        return written;
    }

    /// <summary>Decrypts payload using symmetric stream cipher (ChaCha20).</summary>
    [BenchmarkCategory("Symmetric"), Benchmark]
    public int SymmetricDecrypt()
    {
        SymmetricEngine.Decrypt(_key, _symEnvelope.AsSpan(0, _symWritten), _output, out int written);
        return written;
    }

    /// <summary>Encrypts payload using AEAD cipher (ChaCha20-Poly1305).</summary>
    [BenchmarkCategory("AEAD"), Benchmark(Baseline = true)]
    public int AeadEncrypt()
    {
        AeadEngine.Encrypt(_key, _plaintext, _aeadEnvelope, _nonce12, _aad, 7u, CipherSuiteType.Chacha20Poly1305, out int written);
        return written;
    }

    /// <summary>Decrypts payload using AEAD cipher (ChaCha20-Poly1305).</summary>
    [BenchmarkCategory("AEAD"), Benchmark]
    public int AeadDecrypt()
    {
        AeadEngine.Decrypt(_key, _aeadEnvelope.AsSpan(0, _aeadWritten), _output, _aad, out int written);
        return written;
    }
}