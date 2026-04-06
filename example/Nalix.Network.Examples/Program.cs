// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Microsoft.Extensions.Logging;
using Nalix.Framework.Configuration;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.Logging;
using Nalix.Logging.Options;
using Nalix.Logging.Sinks;
using Nalix.Network.Examples.Attributes;
using Nalix.Network.Examples.Handlers;
using Nalix.Network.Examples.Middleware;
using Nalix.Network.Examples.Protocols;
using Nalix.Network.Hosting;
using Nalix.Network.Options;

internal class Program
{
    private static async Task Main(string[] args)
    {
        // Turn on debug logs so the sample shows the full packet and connection flow.
        ConfigurationManager.Instance.Get<NLogixOptions>()
                            .MinLevel = LogLevel.Trace;

        // Create one logger instance and let the hosting package register it into the shared runtime.
        ILogger logger = new NLogix(cfg => cfg.RegisterTarget(new BatchConsoleLogTarget(t => t.EnableColors = true)));

        using NetworkHost host = NetworkHost.CreateBuilder()
            .UseLogger(logger)
            .Configure<NetworkSocketOptions>(options => options.Port = 57206)
            // Handshake is a built-in frame that lives in Nalix.Framework, so register that assembly explicitly.
            .AddPackets<Handshake>()
            .AddHandlers<PacketCommandHandler>()
            .AddMetadataProvider<PacketTagMetadataProvider>()
            .ConfigureDispatcher(dispatchOptions =>
            {
                _ = dispatchOptions.WithMiddleware(new PacketTagMiddleware());
                _ = dispatchOptions.WithErrorHandling((exception, command) =>
                    logger.Error($"Error handling command: {command}", exception));
            })
            .AddTcp<ExamplePacketProtocol>()
            .Build();

        using CancellationTokenSource shutdown = new();

        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            shutdown.Cancel();
        };

        //Console.WriteLine("Nalix.Network example server is running on tcp://127.0.0.1:57206");
        //Console.WriteLine("Press Ctrl+C to stop.");

        await host.RunAsync(shutdown.Token).ConfigureAwait(false);
    }
}
