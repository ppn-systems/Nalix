// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
using Nalix.Common.Diagnostics;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Logging;
using Nalix.Network.Configurations;
using Nalix.Network.Examples.Custom;
using Nalix.Network.Examples.Handlers;
using Nalix.Network.Examples.Protocols;
using Nalix.Network.Middleware.Inbound;
using Nalix.Network.Routing;
using Nalix.Shared.Frames;

internal class Program
{
    private static void Main(string[] args)
    {
        InstanceManager.Instance.Register<ILogger>(NLogix.Host.Instance);
        IPacketRegistry packetRegistry = new PacketRegistryFactory().CreateCatalog();
        InstanceManager.Instance.Register(packetRegistry);

        // Setup configuration and dependency injection
        NetworkSocketOptions listenerOptions = ConfigurationManager.Instance.Get<NetworkSocketOptions>();
        listenerOptions.Port = 12345; // Set listening port
                                      // ... Additional configuration as needed

        // You can setup configuration from default.ini.
        // Windwow: C:\ProgramData\Nalix\config\default.ini
        // Linux: If XDG_DATA_HOME is not set → use ~/.local/share → for example /home/<Username>/.local/share/Nalix
        // Container (Docker/Kubernetes): /data

        // register the custom metadata provider to enable processing of PacketCustomAttribute on handler methods
        PacketMetadataProviders.Register(new PacketCustomAttributeProvider());


        // IF YOU WANT TO USE PacketDispatchChannel, YOU NEED REGISTER DI IPacketRegistry.
        PacketDispatchChannel channel = new(dispatchOptions =>
        {
            // Inbound
            _ = dispatchOptions.WithMiddleware(new TimeoutMiddleware());

            // Custom middleware
            _ = dispatchOptions.WithMiddleware(new CustomMiddleware());

            // Logging
            // DONT REGISTER LOGGER HERE, IT YOU DONT REGISTER LOGGER BEFOREHAND, IT WILL CAUSE ERROR.
            _ = dispatchOptions.WithLogging(InstanceManager.Instance.GetExistingInstance<ILogger>());

            _ = dispatchOptions.WithErrorHandling((exception, command)
                => InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                           .Error($"Error handling command: {command}", exception));

            // Handlers
            _ = dispatchOptions.WithHandler(() => new PingHandlers());
        });

        AutoXProtocol xProtocol = new(channel);
        AutoXListener xListener = new(xProtocol);

        // Start listening for incoming connections
        xListener.Activate();
        channel.Activate();

        Console.WriteLine(xListener.GenerateReport());
        _ = Console.ReadLine();
    }
}