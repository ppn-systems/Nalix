// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
// Copyright (c) 2026 PPN Corporation. All rights reserved.

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using System.Threading.Tasks;

namespace Nalix.Benchmark.Shared;

public static class Program
{
    public static async Task Main(System.String[] args)
    {
        System.String artifactsPath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "..", "..", "..", "..", "docs", "Nalix.Benchmarks");
        ManualConfig config = DefaultConfig.Instance.WithArtifactsPath(System.IO.Path.GetFullPath(artifactsPath));

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
    }
}