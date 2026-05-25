// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using Nalix.Codec.DataFrames;
using Nalix.LoadTester.Contracts;
using Nalix.LoadTester.Metrics;
using Nalix.LoadTester.Reporting;
using Nalix.LoadTester.Running;
using Nalix.LoadTester.Scenarios;

namespace Nalix.LoadTester;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        if (!LoadTestOptions.TryParse(args, out LoadTestOptions options, out string? error, out Boolean showHelp))
        {
            if (!String.IsNullOrWhiteSpace(error))
            {
                Console.Error.WriteLine(error);
                Console.Error.WriteLine();
            }

            LoadTestOptions.WriteUsage(Console.Error);
            return showHelp ? 0 : 2;
        }

        RuntimeHelpers.RunModuleConstructor(typeof(BenchmarkPacket).Module.ModuleHandle);
        if (!PacketRegistry.IsBuilt)
        {
            PacketRegistry.Build();
        }

        ILoadScenario scenario = options.Scenario switch
        {
            LoadTestScenarioKind.Ping => new PingScenario(options.TimeoutMs),
            LoadTestScenarioKind.Payload => new PayloadEchoScenario(options.TimeoutMs, options.PayloadSize),
            LoadTestScenarioKind.DdosControl => new DdosControlScenario(options.TimeoutMs),
            _ => throw new InvalidOperationException($"Unsupported scenario: {options.Scenario}")
        };

        LatencySampleBuffer samples = new(options.SampleCapacity);
        MetricsCollector metrics = new(samples);
        ConsoleProgressReporter reporter = new();
        LoadTestRunner runner = new(options, scenario, metrics, reporter);

        await runner.RunAsync(CancellationToken.None).ConfigureAwait(false);
        return 0;
    }
}
