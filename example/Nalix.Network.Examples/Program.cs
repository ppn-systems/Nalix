// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
using Nalix.Common.Diagnostics;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.Configuration;
using Nalix.Framework.DataFrames;
using Nalix.Framework.Injection;
using Nalix.Logging;
using Nalix.Logging.Configuration;
using Nalix.Logging.Sinks;
using Nalix.Network.Configurations;
using Nalix.Network.Examples.Attributes;
using Nalix.Network.Examples.Handlers;
using Nalix.Network.Examples.Middleware;
using Nalix.Network.Examples.Protocols;
using Nalix.Network.Middleware.Inbound;
using Nalix.Network.Routing;

internal class Program
{
    private static void Main(string[] args)
    {
        // Turn on debug logs so the sample shows the full packet and connection flow.
        ConfigurationManager.Instance.Get<NLogixOptions>()
                            .MinLevel = LogLevel.Debug;

        // Register a console logger first because the routing pipeline and protocols
        // rely on ILogger being available from the shared container.
        ILogger logger = new NLogix(cfg => cfg.RegisterTarget(new BatchConsoleLogTarget(t => t.EnableColors = true)));
        InstanceManager.Instance.Register(logger);

        // Packet handlers are discovered through the registry, so the sample
        // registers one up front before any protocol starts processing packets.
        IPacketRegistry packetRegistry = new PacketRegistryFactory().CreateCatalog();
        InstanceManager.Instance.Register(packetRegistry);

        // This sets the listening port used by the server example.
        NetworkSocketOptions listenerOptions = ConfigurationManager.Instance.Get<NetworkSocketOptions>();
        listenerOptions.Port = 12345;

        // Register the custom metadata provider so handler annotations are visible to the packet pipeline.
        PacketMetadataProviders.Register(new PacketTagMetadataProvider());

        // The dispatch channel is the "business logic" layer:
        // it wires middleware, error handling, and the packet handlers themselves.
        PacketDispatchChannel channel = new(dispatchOptions =>
        {
            // Timeout middleware should run before custom logic so slow handlers
            // are rejected consistently.
            _ = dispatchOptions.WithLogging(logger);
            _ = dispatchOptions.WithMiddleware(new TimeoutMiddleware());

            // Custom middleware can inspect attributes added by the metadata provider.
            _ = dispatchOptions.WithMiddleware(new PacketTagMiddleware());

            // Route handler failures through the shared logger instead of crashing the sample.
            _ = dispatchOptions.WithErrorHandling((exception, command) =>
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                       .Error($"Error handling command: {command}", exception));

            // Register the object that contains the packet handler methods.
            _ = dispatchOptions.WithHandler(() => new PacketCommandHandler());
        });

        // The protocol bridges the socket listener and the packet dispatch pipeline.
        ExamplePacketProtocol protocol = new(channel);
        ExampleTcpListener listener = new(protocol);

        // Start both layers: the listener accepts connections, the channel handles packets.
        listener.Activate();
        channel.Activate();

        // Print a small runtime report so the user can confirm the listener is alive.
        Console.WriteLine(listener.GenerateReport());
        _ = Console.ReadLine();
    }
}
