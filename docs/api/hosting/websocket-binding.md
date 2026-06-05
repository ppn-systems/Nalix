# WebSocket Hosting Binding

The WebSocket binding system allows developers to host high-performance WebSocket protocol handlers alongside standard TCP and UDP services on a unified `NetworkApplication` host using a fluent configuration builder.

## Overview

The hosting layer wraps `WebSocketListenerBase` initialization behind the builder pattern. To configure and attach a WebSocket server endpoint to the host, you use the `BindWebSocket<TProtocol>()` method on `INetworkApplicationBuilder`, configure options fluently via `IWebSocketBindingBuilder`, and trigger `Bind()`.

## API Reference

### INetworkApplicationBuilder WebSocket Binding Methods

```csharp
namespace Nalix.Hosting;

public interface INetworkApplicationBuilder
{
    // ...
    
    /// <summary>
    /// Binds a WebSocket protocol using a fluent builder for port, path, and factory configuration.
    /// </summary>
    /// <typeparam name="TProtocol">The protocol type to host.</typeparam>
    /// <returns>A fluent builder to configure the WebSocket binding.</returns>
    IWebSocketBindingBuilder BindWebSocket<TProtocol>() where TProtocol : class, IProtocol;
}
```

---

### IWebSocketBindingBuilder Interface

```csharp
namespace Nalix.Hosting;

public interface IWebSocketBindingBuilder
{
    /// <summary>
    /// Sets the port to listen on, overriding the default from NetworkWebSocketOptions.
    /// </summary>
    IWebSocketBindingBuilder OnPort(ushort port);

    /// <summary>
    /// Sets the HTTP path prefix to listen on, overriding the default from NetworkWebSocketOptions.
    /// </summary>
    IWebSocketBindingBuilder WithPath(string path);

    /// <summary>
    /// Uses a custom factory to create protocol instances instead of the default activator.
    /// </summary>
    IWebSocketBindingBuilder WithFactory(Func<IPacketDispatch, IProtocol> factory);

    /// <summary>
    /// Finalizes this binding and returns the parent INetworkApplicationBuilder.
    /// </summary>
    INetworkApplicationBuilder Bind();
}
```

## Usage Example

The following code example shows how to configure a hosted `NetworkApplication` to listen for WebSocket clients on port `8080` under the `/ws` path while utilizing a custom message protocol:

```csharp
using Microsoft.Extensions.Logging;
using Nalix.Hosting;
using Nalix.Network.Routing;
using Nalix.Runtime.Dispatching;
using Nalix.Abstractions.Networking;

public class Program
{
    public static async Task Main(string[] args)
    {
        ILogger logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger("NalixServer");

        // Initialize the network application builder
        INetworkApplicationBuilder builder = NetworkApplication.CreateBuilder();

        builder.ConfigureLogging(logger);

        // Bind our CustomProtocol to a WebSocket endpoint
        builder.BindWebSocket<CustomProtocol>()
               .OnPort(8080)
               .WithPath("/ws")
               .Bind();

        // Build and start the network application host
        using NetworkApplication app = builder.Build();
        await app.ActivateAsync();

        Console.WriteLine("WebSocket server listening on ws://localhost:8080/ws. Press any key to stop...");
        Console.ReadKey();

        await app.DeactivateAsync();
    }
}
```

## See Also

* [WebSocket Listener](../network/websocket-listener.md)
* [WebSocket Connection](../network/websocket-connection.md)
* [WebSocket Options](../options/network/websocket-options.md)
