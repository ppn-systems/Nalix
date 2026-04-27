using BenchmarkDotNet.Running;

namespace Nalix.Benchmark.Framework;

internal static class Program
{
    private static void Main(string[] args)
    {
        _ = BenchmarkSwitcher
            .FromAssembly(typeof(Program).Assembly)
            .Run(args);
    }
}