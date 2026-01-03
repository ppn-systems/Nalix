// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
// Copyright (c) 2026 PPN Corporation. All rights reserved.

// 13th Gen Intel Core i7-13620H 2.40GHz, 1 CPU, 16 logical and 10 physical cores
// .NET SDK 10.0.103
//   [Host]     : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3
//   DefaultJob : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

using System;
using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Order;
using Nalix.Common.Security.Enums;
using Nalix.Shared.Security;

namespace Nalix.Benchmark.Shared.Security;

[RankColumn]
[MemoryDiagnoser]
[DisassemblyDiagnoser]
[MinColumn, MaxColumn]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class EnvelopeCipherBenchmarks
{
    [Params(128, 1024, 8192)]
    public int PayloadSize;

    // Keep all candidates, but handle unsupported ones explicitly in setup.
    [Params(CipherSuiteType.SALSA20, CipherSuiteType.CHACHA20,
            CipherSuiteType.SALSA20_POLY1305, CipherSuiteType.CHACHA20_POLY1305)]
    //[Params(CipherSuiteType.SALSA20, CipherSuiteType.CHACHA20)]
    public CipherSuiteType Algorithm;

    private byte[] _key = [];
    private byte[] _aad = [];
    private byte[] _plaintext = [];
    private byte[] _envelope = [];
    private byte[] _encryptBuffer = [];
    private byte[] _decryptBuffer = [];

    [GlobalSetup]
    public void GlobalSetup()
    {
        _key = new byte[32];
        RandomNumberGenerator.Fill(_key);

        _aad = new byte[16];
        RandomNumberGenerator.Fill(_aad);

        _plaintext = new byte[PayloadSize];
        RandomNumberGenerator.Fill(_plaintext);

        _encryptBuffer = new byte[_plaintext.Length + 64];
        _decryptBuffer = new byte[_plaintext.Length + 64];

        const int overheadMargin = 64;
        byte[] outBuffer = new byte[_plaintext.Length + overheadMargin];

        try
        {
            // Call Encrypt; if algorithm unsupported, it may throw ArgumentException.
            bool success = EnvelopeCipher.Encrypt(
                _key.AsSpan(),
                _plaintext.AsSpan(),
                outBuffer,
                _aad.AsSpan(),
                null,
                Algorithm,
                out int bytesWritten);

            if (!success || bytesWritten <= 0)
            {
                throw new InvalidOperationException($"EnvelopeCipher.Encrypt returned false or wrote 0 bytes for algorithm {Algorithm}.");
            }

            _envelope = new byte[bytesWritten];
            Array.Copy(outBuffer, 0, _envelope, 0, bytesWritten);
        }
        catch (ArgumentException aex) when (aex.ParamName == "type" || aex.Message.Contains("Unsupported symmetric algorithm"))
        {
            // Fail fast with a clearer diagnostic message so BenchmarkDotNet will show stack trace instead of NA.
            throw new InvalidOperationException($"Algorithm {Algorithm} is not supported by EnvelopeCipher. Inner: {aex.Message}", aex);
        }
    }

    [Benchmark(Description = "EnvelopeCipher.Encrypt")]
    public bool Encrypt()
    {
        return EnvelopeCipher.Encrypt(
            _key.AsSpan(),
            _plaintext.AsSpan(),
            _encryptBuffer,
            _aad.AsSpan(),
            null,
            Algorithm,
            out _);
    }

    [Benchmark(Description = "EnvelopeCipher.Decrypt")]
    public bool Decrypt()
    {
        return EnvelopeCipher.Decrypt(
            _key.AsSpan(),
            _envelope.AsSpan(),
            _decryptBuffer,
            out _);
    }
}
