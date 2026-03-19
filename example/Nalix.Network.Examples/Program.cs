using Nalix.Common.Diagnostics.Abstractions;
using Nalix.Common.Networking.Packets.Abstractions;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Logging;
using Nalix.Network.Configurations;
using Nalix.Network.Examples.Custom;
using Nalix.Network.Examples.Handlers;
using Nalix.Network.Examples.Protocols;
using Nalix.Network.Middleware.Inbound;
using Nalix.Network.Routing;
using Nalix.Network.Routing.Metadata;
using Nalix.Shared.Registry;

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
    dispatchOptions.WithMiddleware(new TimeoutMiddleware());

    // Custom middleware
    dispatchOptions.WithMiddleware(new CustomMiddleware());

    // Logging
    // DONT REGISTER LOGGER HERE, IT YOU DONT REGISTER LOGGER BEFOREHAND, IT WILL CAUSE ERROR.
    dispatchOptions.WithLogging(InstanceManager.Instance.GetExistingInstance<ILogger>());

    dispatchOptions.WithErrorHandling((exception, command)
        => InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                   .Error($"Error handling command: {command}", exception));

    // Handlers
    dispatchOptions.WithHandler(() => new PingHandlers());
});

AutoXProtocol xProtocol = new(channel);
AutoXListener xListener = new(xProtocol);

// Start listening for incoming connections
xListener.Activate();

Console.WriteLine(xListener.GenerateReport());
Console.ReadLine();