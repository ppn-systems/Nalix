// Copyright (c) 2026 PPN Corporation. All rights reserved.

using BenchmarkDotNet.Running;
using Nalix.Benchmark.Shared.Security.Asymmetric;

namespace Nalix.Benchmark.Shared;

public static class Program
{
    public static void Main(System.String[] args) => BenchmarkRunner.Run<X25519Benchmarks>();
}