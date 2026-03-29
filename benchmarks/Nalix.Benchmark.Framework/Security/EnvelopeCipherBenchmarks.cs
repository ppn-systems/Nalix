// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using BenchmarkDotNet.Attributes;
using Nalix.Common.Security;
using Nalix.Framework.Security;

namespace Nalix.Benchmark.Framework.Security;

/// <summary>
/// Đo hiệu năng <see cref="EnvelopeCipher"/> trên 4 cipher suites:
/// <list type="bullet">
///   <item>ChaCha20        — stream cipher, không tag</item>
///   <item>ChaCha20-Poly1305 — AEAD, có authentication tag</item>
///   <item>Salsa20         — stream cipher, không tag</item>
///   <item>Salsa20-Poly1305  — AEAD, có authentication tag</item>
/// </list>
/// Mỗi suite đo Encrypt + Decrypt ở payload nhỏ (64 B) và vừa (1024 B).
/// </summary>
[MemoryDiagnoser]
[Config(typeof(BenchmarkConfig))]
public class EnvelopeCipherBenchmarks
{
    // -----------------------------------------------------------------------
    // Constants
    // -----------------------------------------------------------------------

    // Overhead tối đa của envelope: HeaderSize + NonceSize + TagSize
    // Dùng 128 để an toàn cho cả AEAD lẫn stream suite
    private const int EnvelopeOverhead = 128;

    // -----------------------------------------------------------------------
    // Fields
    // -----------------------------------------------------------------------

    private byte[] _key = null!;
    private byte[] _aad = null!;

    // Plaintext buffers — được fill 1 lần trong GlobalSetup
    private byte[] _plaintext64 = null!;
    private byte[] _plaintext1024 = null!;

    // Ciphertext output buffers — sized = payload + overhead
    private byte[] _ciphertext64 = null!;
    private byte[] _ciphertext1024 = null!;

    // Envelope buffers (kết quả Encrypt) — dùng làm input cho Decrypt
    private byte[] _envelope64_chacha20 = null!;
    private byte[] _envelope64_chacha20poly1305 = null!;
    private byte[] _envelope64_salsa20 = null!;
    private byte[] _envelope64_salsa20poly1305 = null!;

    private byte[] _envelope1024_chacha20 = null!;
    private byte[] _envelope1024_chacha20poly1305 = null!;
    private byte[] _envelope1024_salsa20 = null!;
    private byte[] _envelope1024_salsa20poly1305 = null!;

    // Plaintext output buffer dùng cho Decrypt
    private byte[] _decryptOut = null!;

    // -----------------------------------------------------------------------
    // Params
    // -----------------------------------------------------------------------

    /// <summary>
    /// Payload size tính bằng bytes.
    /// 64 B = typical small packet; 1024 B = typical medium frame.
    /// </summary>
    [Params(64, 1024)]
    public int PayloadBytes { get; set; }

    // -----------------------------------------------------------------------
    // Setup / Cleanup
    // -----------------------------------------------------------------------

    [GlobalSetup]
    public void Setup()
    {
        // Key 32 bytes — đủ cho tất cả suites
        _key = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(_key);

        // AAD nhỏ, chỉ dùng cho AEAD benchmarks
        _aad = new byte[16];
        System.Security.Cryptography.RandomNumberGenerator.Fill(_aad);

        _plaintext64 = new byte[64];
        _plaintext1024 = new byte[1024];
        System.Security.Cryptography.RandomNumberGenerator.Fill(_plaintext64);
        System.Security.Cryptography.RandomNumberGenerator.Fill(_plaintext1024);

        _ciphertext64 = new byte[64 + EnvelopeOverhead];
        _ciphertext1024 = new byte[1024 + EnvelopeOverhead];
        _decryptOut = new byte[1024 + EnvelopeOverhead];

        // Pre-encrypt một lần để có envelope hợp lệ cho Decrypt benchmarks
        _envelope64_chacha20 = this.Encrypt64(CipherSuiteType.Chacha20, withAad: false);
        _envelope64_chacha20poly1305 = this.Encrypt64(CipherSuiteType.Chacha20Poly1305, withAad: true);
        _envelope64_salsa20 = this.Encrypt64(CipherSuiteType.Salsa20, withAad: false);
        _envelope64_salsa20poly1305 = this.Encrypt64(CipherSuiteType.Salsa20Poly1305, withAad: true);

        _envelope1024_chacha20 = this.Encrypt1024(CipherSuiteType.Chacha20, withAad: false);
        _envelope1024_chacha20poly1305 = this.Encrypt1024(CipherSuiteType.Chacha20Poly1305, withAad: true);
        _envelope1024_salsa20 = this.Encrypt1024(CipherSuiteType.Salsa20, withAad: false);
        _envelope1024_salsa20poly1305 = this.Encrypt1024(CipherSuiteType.Salsa20Poly1305, withAad: true);
    }

    // Helper: encrypt 64-byte plaintext, trả về trimmed envelope
    private byte[] Encrypt64(CipherSuiteType suite, bool withAad)
    {
        byte[] buf = new byte[64 + EnvelopeOverhead];
        EnvelopeCipher.Encrypt(
            _key, _plaintext64, buf,
            withAad ? _aad : default,
            seq: null, suite, out int written);
        return buf[..written];
    }

    // Helper: encrypt 1024-byte plaintext, trả về trimmed envelope
    private byte[] Encrypt1024(CipherSuiteType suite, bool withAad)
    {
        byte[] buf = new byte[1024 + EnvelopeOverhead];
        EnvelopeCipher.Encrypt(
            _key, _plaintext1024, buf,
            withAad ? _aad : default,
            seq: null, suite, out int written);
        return buf[..written];
    }

    // Chọn đúng plaintext / envelope buffer theo PayloadBytes param
    private ReadOnlySpan<byte> Plaintext
        => this.PayloadBytes == 64 ? _plaintext64 : _plaintext1024;

    private Span<byte> CiphertextBuf
        => this.PayloadBytes == 64 ? _ciphertext64 : _ciphertext1024;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "<Pending>")]
    private ReadOnlySpan<byte> Envelope(CipherSuiteType suite, bool withAad) => suite switch
    {
        CipherSuiteType.Chacha20 => this.PayloadBytes == 64 ? _envelope64_chacha20 : _envelope1024_chacha20,
        CipherSuiteType.Chacha20Poly1305 => this.PayloadBytes == 64 ? _envelope64_chacha20poly1305 : _envelope1024_chacha20poly1305,
        CipherSuiteType.Salsa20 => this.PayloadBytes == 64 ? _envelope64_salsa20 : _envelope1024_salsa20,
        CipherSuiteType.Salsa20Poly1305 => this.PayloadBytes == 64 ? _envelope64_salsa20poly1305 : _envelope1024_salsa20poly1305,
        _ => throw new ArgumentOutOfRangeException()
    };

    // -----------------------------------------------------------------------
    // ENCRYPT benchmarks — ChaCha20
    // -----------------------------------------------------------------------

    /// <summary>
    /// Encrypt stream-only (không tag, không AAD).
    /// Path: SymmetricEngine → ChaCha20 CTR → header + nonce + ciphertext.
    /// </summary>
    [BenchmarkCategory("Encrypt", "Stream")]
    [Benchmark(Description = "Encrypt — ChaCha20 (stream, no tag)")]
    public int Encrypt_ChaCha20()
    {
        EnvelopeCipher.Encrypt(
            _key, this.Plaintext, this.CiphertextBuf,
            seq: null, CipherSuiteType.Chacha20, out int written);
        return written;
    }

    /// <summary>
    /// Encrypt AEAD với AAD.
    /// Path: AeadEngine → ChaCha20-Poly1305 → header + nonce + ciphertext + tag.
    /// Kỳ vọng: chậm hơn stream vì có thêm Poly1305 MAC computation.
    /// </summary>
    [BenchmarkCategory("Encrypt", "AEAD")]
    [Benchmark(Description = "Encrypt — ChaCha20-Poly1305 (AEAD + AAD)")]
    public int Encrypt_ChaCha20Poly1305()
    {
        EnvelopeCipher.Encrypt(
            _key, this.Plaintext, this.CiphertextBuf,
            _aad, seq: null, CipherSuiteType.Chacha20Poly1305, out int written);
        return written;
    }

    // -----------------------------------------------------------------------
    // ENCRYPT benchmarks — Salsa20
    // -----------------------------------------------------------------------

    /// <summary>
    /// Salsa20 stream — baseline để so sánh với ChaCha20 (cùng họ, khác permutation).
    /// </summary>
    [BenchmarkCategory("Encrypt", "Stream")]
    [Benchmark(Description = "Encrypt — Salsa20 (stream, no tag)")]
    public int Encrypt_Salsa20()
    {
        EnvelopeCipher.Encrypt(
            _key, this.Plaintext, this.CiphertextBuf,
            seq: null, CipherSuiteType.Salsa20, out int written);
        return written;
    }

    /// <summary>
    /// Salsa20-Poly1305 AEAD — so sánh MAC overhead vs ChaCha20-Poly1305.
    /// </summary>
    [BenchmarkCategory("Encrypt", "AEAD")]
    [Benchmark(Description = "Encrypt — Salsa20-Poly1305 (AEAD + AAD)")]
    public int Encrypt_Salsa20Poly1305()
    {
        EnvelopeCipher.Encrypt(
            _key, this.Plaintext, this.CiphertextBuf,
            _aad, seq: null, CipherSuiteType.Salsa20Poly1305, out int written);
        return written;
    }

    // -----------------------------------------------------------------------
    // DECRYPT benchmarks — ChaCha20
    // -----------------------------------------------------------------------

    /// <summary>
    /// Decrypt stream (ChaCha20).
    /// Path: EnvelopeFormat.TryParseEnvelope → SymmetricEngine.Decrypt.
    /// </summary>
    [BenchmarkCategory("Decrypt", "Stream")]
    [Benchmark(Description = "Decrypt — ChaCha20 (stream, no tag)")]
    public int Decrypt_ChaCha20()
    {
        EnvelopeCipher.Decrypt(
            _key, this.Envelope(CipherSuiteType.Chacha20, withAad: false),
            _decryptOut, out int written);
        return written;
    }

    /// <summary>
    /// Decrypt AEAD (ChaCha20-Poly1305).
    /// Path: TryParseEnvelope → AeadEngine.Decrypt → Poly1305 tag verify.
    /// Kỳ vọng: chậm hơn stream vì phải verify MAC trước khi giải mã.
    /// </summary>
    [BenchmarkCategory("Decrypt", "AEAD")]
    [Benchmark(Description = "Decrypt — ChaCha20-Poly1305 (AEAD + AAD verify)")]
    public int Decrypt_ChaCha20Poly1305()
    {
        EnvelopeCipher.Decrypt(
            _key, this.Envelope(CipherSuiteType.Chacha20Poly1305, withAad: true),
            _decryptOut, _aad, out int written);
        return written;
    }

    // -----------------------------------------------------------------------
    // DECRYPT benchmarks — Salsa20
    // -----------------------------------------------------------------------

    /// <summary>
    /// Decrypt stream (Salsa20).
    /// </summary>
    [BenchmarkCategory("Decrypt", "Stream")]
    [Benchmark(Description = "Decrypt — Salsa20 (stream, no tag)")]
    public int Decrypt_Salsa20()
    {
        EnvelopeCipher.Decrypt(
            _key, this.Envelope(CipherSuiteType.Salsa20, withAad: false),
            _decryptOut, out int written);
        return written;
    }

    /// <summary>
    /// Decrypt AEAD (Salsa20-Poly1305).
    /// </summary>
    [BenchmarkCategory("Decrypt", "AEAD")]
    [Benchmark(Description = "Decrypt — Salsa20-Poly1305 (AEAD + AAD verify)")]
    public int Decrypt_Salsa20Poly1305()
    {
        EnvelopeCipher.Decrypt(
            _key, this.Envelope(CipherSuiteType.Salsa20Poly1305, withAad: true),
            _decryptOut, _aad, out int written);
        return written;
    }
}
