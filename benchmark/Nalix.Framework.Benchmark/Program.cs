// Copyright (c) 2025 PPN Corporation. All rights reserved.

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

namespace Nalix.Framework.Benchmarks
{
    public class Program
    {
        public static void Main(System.String[] args)
        {
            System.Boolean disableValidator = System.Environment.GetEnvironmentVariable("DISABLE_BENCH_OPTIMIZATIONS_VALIDATOR") == "1";

            if (disableValidator)
            {
                ManualConfig config = ManualConfig.Create(DefaultConfig.Instance)
                                                  .WithOptions(ConfigOptions.DisableOptimizationsValidator);
                _ = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
            }
            else
            {
                _ = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
            }
        }
    }
}