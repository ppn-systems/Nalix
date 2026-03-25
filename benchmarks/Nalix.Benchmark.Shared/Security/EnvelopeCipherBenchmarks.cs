// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
// Copyright (c) 2026 PPN Corporation. All rights reserved.

// 13th Gen Intel Core i7-13620H 2.40GHz, 1 CPU, 16 logical and 10 physical cores
// .NET SDK 10.0.103
//   [Host]     : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3
//   DefaultJob : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Order;
using Nalix.Common.Security.Enums;
using Nalix.Shared.Security;
using System;
using System.Security.Cryptography;

namespace Nalix.Benchmark.Shared.Security;

[RankColumn]
[MemoryDiagnoser]
[DisassemblyDiagnoser]
[MinColumn, MaxColumn]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class EnvelopeCipherBenchmarks
{
    [Params(128, 1024, 8192)]
    public Int32 PayloadSize;

    // Keep all candidates, but handle unsupported ones explicitly in setup.
    [Params(CipherSuiteType.SALSA20, CipherSuiteType.CHACHA20,
            CipherSuiteType.SALSA20_POLY1305, CipherSuiteType.CHACHA20_POLY1305)]
    //[Params(CipherSuiteType.SALSA20, CipherSuiteType.CHACHA20)]
    public CipherSuiteType Algorithm;

    private Byte[] _key = Array.Empty<Byte>();
    private Byte[] _aad = Array.Empty<Byte>();
    private Byte[] _plaintext = Array.Empty<Byte>();
    private Byte[] _envelope = Array.Empty<Byte>();
    private Byte[] _encryptBuffer = Array.Empty<Byte>();
    private Byte[] _decryptBuffer = Array.Empty<Byte>();

    [GlobalSetup]
    public void GlobalSetup()
    {
        _key = new Byte[32];
        RandomNumberGenerator.Fill(_key);

        _aad = new Byte[16];
        RandomNumberGenerator.Fill(_aad);

        _plaintext = new Byte[PayloadSize];
        RandomNumberGenerator.Fill(_plaintext);

        _encryptBuffer = new Byte[_plaintext.Length + 64];
        _decryptBuffer = new Byte[_plaintext.Length + 64];

        const Int32 overheadMargin = 64;
        var outBuffer = new Byte[_plaintext.Length + overheadMargin];

        try
        {
            // Call Encrypt; if algorithm unsupported, it may throw ArgumentException.
            Boolean success = EnvelopeCipher.Encrypt(
                _key.AsSpan(),
                _plaintext.AsSpan(),
                outBuffer,
                _aad.AsSpan(),
                null,
                Algorithm,
                out Int32 bytesWritten);

            if (!success || bytesWritten <= 0)
            {
                throw new InvalidOperationException($"EnvelopeCipher.Encrypt returned false or wrote 0 bytes for algorithm {Algorithm}.");
            }

            _envelope = new Byte[bytesWritten];
            Array.Copy(outBuffer, 0, _envelope, 0, bytesWritten);
        }
        catch (ArgumentException aex) when (aex.ParamName == "type" || aex.Message.Contains("Unsupported symmetric algorithm"))
        {
            // Fail fast with a clearer diagnostic message so BenchmarkDotNet will show stack trace instead of NA.
            throw new InvalidOperationException($"Algorithm {Algorithm} is not supported by EnvelopeCipher. Inner: {aex.Message}", aex);
        }
    }

    [Benchmark(Description = "EnvelopeCipher.Encrypt")]
    public Boolean Encrypt()
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
    public Boolean Decrypt()
    {
        return EnvelopeCipher.Decrypt(
            _key.AsSpan(),
            _envelope.AsSpan(),
            _decryptBuffer,
            out _);
    }
}