// Copyright (c) 2026 PPN Corporation. All rights reserved.

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

namespace Nalix.Benchmark.Shared;

public static class Program
{
    public static void Main(System.String[] args)
    {
        System.String artifactsPath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "..", "..", "..", "..", "docs", "Nalix.Benchmarks");
        ManualConfig config = DefaultConfig.Instance.WithArtifactsPath(System.IO.Path.GetFullPath(artifactsPath));

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
    }
}