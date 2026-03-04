// Copyright (c) 2026 PPN Corporation. All rights reserved.

// 13th Gen Intel Core i7-13620H 2.40GHz, 1 CPU, 16 logical and 10 physical cores
// .NET SDK 10.0.103
//   [Host]     : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3
//   Job-WCSQUR : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

// | Method                    | Algorithm         | Mean     | Error    | StdDev   | Median   | Code Size | Allocated |
// |-------------------------- |------------------ |---------:|---------:|---------:|---------:|----------:|----------:|
// | EnvelopeEncryptor.Encrypt | SPECK             | 36.36 us | 0.868 us | 2.519 us | 35.60 us |   1,879 B |  17.05 KB |
// | EnvelopeEncryptor.Decrypt | SPECK             | 26.37 us | 0.525 us | 0.919 us | 26.30 us |   3,238 B |   8.53 KB |
// | EnvelopeEncryptor.Encrypt | SALSA20           | 51.30 us | 1.022 us | 2.349 us | 51.05 us |   1,879 B |  12.86 KB |
// | EnvelopeEncryptor.Decrypt | SALSA20           | 27.98 us | 0.562 us | 1.325 us | 27.60 us |   3,238 B |   4.78 KB |
// | EnvelopeEncryptor.Encrypt | CHACHA20          | 37.55 us | 1.017 us | 2.934 us | 37.55 us |   1,879 B |  14.26 KB |
// | EnvelopeEncryptor.Decrypt | CHACHA20          | 26.46 us | 0.517 us | 0.708 us | 26.30 us |   3,238 B |   5.91 KB |
// | EnvelopeEncryptor.Encrypt | SPECK_POLY1305    | 54.46 us | 1.092 us | 2.698 us | 53.70 us |   1,879 B |  19.07 KB |
// | EnvelopeEncryptor.Decrypt | SPECK_POLY1305    | 47.46 us | 0.940 us | 1.596 us | 47.10 us |   3,238 B |  12.28 KB |
// | EnvelopeEncryptor.Encrypt | SALSA20_POLY1305  | 45.25 us | 0.902 us | 1.862 us | 44.60 us |   1,879 B |   11.2 KB |
// | EnvelopeEncryptor.Decrypt | SALSA20_POLY1305  | 38.97 us | 0.727 us | 0.607 us | 38.80 us |   3,238 B |   4.78 KB |
// | EnvelopeEncryptor.Encrypt | CHACHA20_POLY1305 | 56.17 us | 1.466 us | 4.254 us | 55.35 us |   1,879 B |  11.45 KB |
// | EnvelopeEncryptor.Decrypt | CHACHA20_POLY1305 | 48.75 us | 1.022 us | 2.883 us | 47.70 us |   3,238 B |   4.78 KB |

using BenchmarkDotNet.Attributes;
using Nalix.Common.Attributes;
using Nalix.Common.Enums;
using Nalix.Shared.Security;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace Nalix.Benchmark.Shared.Security;

// Memory diagnoser to capture allocations; tune job/runtime as needed.
[MemoryDiagnoser]
[DisassemblyDiagnoser]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0301:Simplify collection initialization", Justification = "<Pending>")]
public class EnvelopeEncryptorBenchmarks
{
    private Byte[] _key = Array.Empty<Byte>();
    private readonly Byte[] _aad = Array.Empty<Byte>();

    [Params(CipherSuiteType.SPECK, CipherSuiteType.SALSA20, CipherSuiteType.CHACHA20,
            CipherSuiteType.SPECK_POLY1305, CipherSuiteType.SALSA20_POLY1305, CipherSuiteType.CHACHA20_POLY1305)]
    public CipherSuiteType Algorithm;

    // Instances used for each benchmark iteration.
    private SampleModel _plainInstance = null!;
    private SampleModel _instanceToDecrypt = null!;

    // Global setup executed once per benchmark run
    [GlobalSetup]
    public void GlobalSetup()
    {
        // Create a key of the required length for the chosen algorithm (32 bytes for CHACHA20_POLY1305)
        _key = new Byte[32];
        RandomNumberGenerator.Fill(_key);

        _plainInstance = CreateSample();
    }

    // Prepare a fresh plain instance before each Encrypt iteration to avoid encrypting an already-encrypted object.
    [IterationSetup(Target = nameof(EncryptObject))]
    public void SetupForEncrypt() => _plainInstance = CreateSample();

    // Prepare an already-encrypted instance before each Decrypt iteration.
    // The pre-encryption is done outside the measured benchmark so Decrypt timing is isolated.
    [IterationSetup(Target = nameof(DecryptObject))]
    public void SetupForDecrypt()
    {
        _instanceToDecrypt = CreateSample();
        EnvelopeEncryptor.Encrypt(_instanceToDecrypt, _key, Algorithm, _aad);
    }

    // Benchmark: Encrypt a populated object graph
    [Benchmark(Description = "EnvelopeEncryptor.Encrypt")]
    public void EncryptObject() =>
        // Encrypt in-place; EnvelopeEncryptor mutates object and may set IPacket flags etc.
        EnvelopeEncryptor.Encrypt(_plainInstance, _key, Algorithm, _aad);

    // Benchmark: Decrypt the previously encrypted object graph
    [Benchmark(Description = "EnvelopeEncryptor.Decrypt")]
    public void DecryptObject() => EnvelopeEncryptor.Decrypt(_instanceToDecrypt, _key, _aad);

    // Factory that creates a representative object graph containing:
    // - strings (sensitive)
    // - list of nested objects
    // - array of nested objects
    // - value-type member (int) marked sensitive
    private static SampleModel CreateSample()
    {
        var root = new SampleModel
        {
            Id = Guid.NewGuid(),
            PublicInfo = "This is public",
            SensitiveString = "Very sensitive text that should be encrypted",
            SensitiveNumber = 42,
            Nested = new ChildModel
            {
                SensitiveChildString = "Nested sensitive text",
                ChildNumber = 7
            },
            ChildrenList =
            [
                new ChildModel { SensitiveChildString = "list[0] sensitive", ChildNumber = 1 },
                new ChildModel { SensitiveChildString = "list[1] sensitive", ChildNumber = 2 }
            ],
            ChildrenArray =
            [
                new() { SensitiveChildString = "array[0] sensitive", ChildNumber = 10 },
                new() { SensitiveChildString = "array[1] sensitive", ChildNumber = 11 }
            ]
        };

        return root;
    }
}

// Sample model with a mix of sensitive and non-sensitive members.
// Attributes and enums are expected to come from the Nalix.* namespaces used by EnvelopeEncryptor.
public class SampleModel
{
    // Public non-sensitive member
    public Guid Id { get; set; }

    public String PublicInfo { get; set; } = String.Empty;

    // Sensitive string — will be encrypted
    [SensitiveData(Level = DataSensitivityLevel.Confidential)]
    public String SensitiveString { get; set; } = String.Empty;

    // Sensitive value-type — stored externally by EnvelopeEncryptor
    [SensitiveData(Level = DataSensitivityLevel.Confidential)]
    public Int32 SensitiveNumber;

    // Nested reference object that also has sensitive members
    [SensitiveData(Level = DataSensitivityLevel.Confidential)]
    public ChildModel Nested { get; set; }

    // List of nested objects; EnvelopeEncryptor will traverse and encrypt elements
    [SensitiveData(Level = DataSensitivityLevel.Confidential)]
    public List<ChildModel> ChildrenList { get; set; }

    // Array of nested objects
    [SensitiveData(Level = DataSensitivityLevel.Confidential)]
    public ChildModel[] ChildrenArray { get; set; }
}

public class ChildModel
{
    [SensitiveData(Level = DataSensitivityLevel.Confidential)]
    public String SensitiveChildString { get; set; } = String.Empty;

    [SensitiveData(Level = DataSensitivityLevel.Confidential)]
    public Int32 ChildNumber { get; set; }
}