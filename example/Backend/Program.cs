// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Nalix.Hosting;

namespace Backend;

[DebuggerStepThrough]
[ExcludeFromCodeCoverage]
public static class Program
{
    [STAThread]
    [SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "Async disposal belongs to the console entry point lifecycle.")]
    public static async Task<int> Main()
    {
        ILogger logger = Startup.CreateBootstrapLogger();
        using CancellationTokenSource exit = new();

        await using NetworkApplication host = Startup.Configure(logger);

        await host.RunAsync(exit.Token).ConfigureAwait(false);

        return 0;
    }
}

