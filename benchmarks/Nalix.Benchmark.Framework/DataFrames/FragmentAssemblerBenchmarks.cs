using BenchmarkDotNet.Attributes;
using Nalix.Benchmark.Framework.Abstractions;
using Nalix.Framework.DataFrames.Chunks;

namespace Nalix.Benchmark.Framework.DataFrames;

/// <summary>
/// Benchmarks for fragment assembly performance and frame detection.
/// </summary>
public class FragmentAssemblerBenchmarks : NalixBenchmarkBase
{
    private byte[][] _chunks = null!;
    private FragmentHeader[] _headers = null!;
    private byte[] _fragmentedPayload = null!;

    [Params(4, 16)]
    public int ChunkCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        const int chunkSize = 256;
        _headers = new FragmentHeader[ChunkCount];
        _chunks = new byte[ChunkCount][];
        _fragmentedPayload = new byte[FragmentHeader.WireSize + chunkSize];

        for (int i = 0; i < ChunkCount; i++)
        {
            _headers[i] = new FragmentHeader(1, (ushort)i, (ushort)ChunkCount, i == ChunkCount - 1);
            _chunks[i] = new byte[chunkSize];
            for (int j = 0; j < chunkSize; j++)
            {
                _chunks[i][j] = (byte)((i + j) % 241);
            }
        }

        _headers[0].WriteTo(_fragmentedPayload);
    }

    [Benchmark]
    public int AssembleSequentialChunks()
    {
        using FragmentAssembler assembler = new();
        FragmentAssemblyResult? result = null;
        try
        {
            for (int i = 0; i < _headers.Length; i++)
            {
                result = assembler.Add(_headers[i], _chunks[i], out _);
            }

            return result?.Length ?? 0;
        }
        finally
        {
            result?.Lease.Dispose();
        }
    }

    [Benchmark]
    public bool DetectFragmentedFrame()
        => FragmentAssembler.IsFragmentedFrame(_fragmentedPayload, out _);
}
