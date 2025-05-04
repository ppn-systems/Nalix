using BenchmarkDotNet.Running;
using Nalix.Benchmark.Package;

namespace Nalix.Benchmark;

internal class Program
{
    private static void Main(string[] args)
    {
        BenchmarkRunner.Run<PacketBenchmark>();
        BenchmarkRunner.Run<PacketCompressionBenchmark>();
        BenchmarkRunner.Run<PacketSerializationBenchmark>();
    }
}
