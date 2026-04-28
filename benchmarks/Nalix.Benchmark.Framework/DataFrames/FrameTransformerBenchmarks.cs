using BenchmarkDotNet.Attributes;
using Nalix.Benchmark.Framework.Abstractions;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Primitives;
using Nalix.Abstractions.Security;
using Nalix.Codec.DataFrames.SignalFrames;
using Nalix.Codec.Transforms;
using Nalix.Codec.Memory;
using Nalix.Environment.Random;

namespace Nalix.Benchmark.Framework.DataFrames;

/// <summary>
/// Benchmarks for frame transformations including compression and encryption.
/// </summary>
public class FrameTransformerBenchmarks : NalixBenchmarkBase
{
    private readonly byte[] _key = new byte[32];

    private byte[] _rawPacket = null!;
    private BufferLease _source = null!;
    private BufferLease _compressed = null!;
    private BufferLease _encrypted = null!;

    [Params(64, 256)]
    public int PayloadBytes { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        for (int i = 0; i < _key.Length; i++)
        {
            _key[i] = (byte)(i + 1);
        }

        Handshake frame = new();
        frame.Initialize(
            HandshakeStage.SERVER_HELLO,
            new Bytes32(Csprng.GetBytes(32)),
            new Bytes32(Csprng.GetBytes(32)),
            new Bytes32(Csprng.GetBytes(32)),
            PacketFlags.RELIABLE);
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

    [BenchmarkCategory("Transformation"), Benchmark(Description = "Compress Frame (LZ4)")]
    public int CompressFrame()
    {
        FrameTransformer.Compress(_source, _compressed);
        return _compressed.Length;
    }

    [BenchmarkCategory("Transformation"), Benchmark(Description = "Encrypt Frame (ChaCha20-Poly1305)")]
    public int EncryptFrame()
    {
        FrameTransformer.Encrypt(_source, _encrypted, _key, CipherSuiteType.Chacha20Poly1305);
        return _encrypted.Length;
    }
}