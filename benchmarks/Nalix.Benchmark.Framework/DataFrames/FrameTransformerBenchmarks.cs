using System;
using BenchmarkDotNet.Attributes;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Security;
using Nalix.Framework.DataFrames;
using Nalix.Framework.DataFrames.TextFrames;
using Nalix.Framework.Memory.Buffers;

namespace Nalix.Benchmark.Framework.DataFrames;

[MemoryDiagnoser]
[Config(typeof(BenchmarkConfig))]
public class FrameTransformerBenchmarks
{
    private readonly byte[] _key = new byte[32];

    private byte[] _rawPacket;
    private BufferLease _source;
    private BufferLease _compressed;
    private BufferLease _encrypted;

    [Params(64, 256)]
    public int PayloadBytes { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        for (int i = 0; i < _key.Length; i++)
        {
            _key[i] = (byte)(i + 1);
        }

        Text256 frame = new();
        frame.Initialize(new string('a', this.PayloadBytes), ProtocolType.TCP);
        frame.Flags = PacketFlags.NONE;
        _rawPacket = frame.Serialize();

        _source = BufferLease.CopyFrom(_rawPacket);
        _compressed = BufferLease.Rent(FrameTransformer.Offset + FrameTransformer.GetMaxCompressedSize(_rawPacket.Length - FrameTransformer.Offset));
        _encrypted = BufferLease.Rent(FrameTransformer.Offset + FrameTransformer.GetMaxCiphertextSize(CipherSuiteType.Chacha20Poly1305, _rawPacket.Length - FrameTransformer.Offset));
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _source.Dispose();
        _compressed.Dispose();
        _encrypted.Dispose();
    }

    [IterationSetup]
    public void ResetBuffers()
    {
        _source.CommitLength(_rawPacket.Length);
        _rawPacket.AsSpan().CopyTo(_source.SpanFull);
        _compressed.CommitLength(0);
        _encrypted.CommitLength(0);
    }

    [Benchmark]
    public int Compress()
    {
        FrameTransformer.Compress(_source, _compressed);
        return _compressed.Length;
    }

    [Benchmark]
    public int Encrypt()
    {
        FrameTransformer.Encrypt(_source, _encrypted, _key, CipherSuiteType.Chacha20Poly1305);
        return _encrypted.Length;
    }
}
