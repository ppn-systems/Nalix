using BenchmarkDotNet.Running;

namespace Nalix.Framework.Benchmark;

public static class Program
{
    public static void Main(System.String[] args) => BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}
