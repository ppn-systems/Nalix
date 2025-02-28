# PacketDispatcher Class Documentation

The `PacketDispatcher` class is an ultra-high performance packet dispatcher with advanced dependency injection (DI) integration and async support. This implementation uses reflection to map packet command IDs to controller methods.

## Namespace

```csharp
using Notio.Common.Connection;
using Notio.Common.Package;
using System;
using System.Threading.Tasks;
```

## Class Definition

### Summary

The `PacketDispatcher` class processes incoming packets and invokes corresponding handlers based on the registered command IDs. It logs errors and warnings when handling failures or unregistered commands.

```csharp
namespace Notio.Network.Handlers
{
    /// <summary>
    /// Ultra-high performance packet dispatcher with advanced dependency injection (DI) integration and async support.
    /// This implementation uses reflection to map packet command IDs to controller methods.
    /// </summary>
    /// <remarks>
    /// The <see cref="PacketDispatcher"/> processes incoming packets and invokes corresponding handlers
    /// based on the registered command IDs. It logs errors and warnings when handling failures or unregistered commands.
    /// </remarks>
    /// <param name="options">
    /// A delegate used to configure <see cref="PacketDispatcherOptions"/> before processing packets.
    /// </param>
    public class PacketDispatcher(Action<PacketDispatcherOptions> options)
        : PacketDispatcherBase(options), IPacketDispatcher
    {
        // Class implementation...
    }
}
```

## Methods

### HandlePacket (byte[]?, IConnection)

```csharp
public void HandlePacket(byte[]? packet, IConnection connection)
```

- **Description**: Handles an incoming packet represented as a byte array.
- **Parameters**:
  - `packet`: The packet data as a byte array.
  - `connection`: The connection from which the packet was received.
- **Remarks**: If the packet or deserialization method is null, an error is logged.

### HandlePacket (ReadOnlyMemory<`byte`>?, IConnection)

```csharp
public void HandlePacket(ReadOnlyMemory<byte>? packet, IConnection connection)
```

- **Description**: Handles an incoming packet represented as a read-only memory segment.
- **Parameters**:
  - `packet`: The packet data as a read-only memory segment.
  - `connection`: The connection from which the packet was received.
- **Remarks**: If the packet or deserialization method is null, an error is logged.

### HandlePacket (IPacket?, IConnection)

```csharp
public void HandlePacket(IPacket? packet, IConnection connection)
```

- **Description**: Handles an incoming packet represented as an `IPacket` object.
- **Parameters**:
  - `packet`: The packet data as an `IPacket` object.
  - `connection`: The connection from which the packet was received.
- **Remarks**: Logs errors if the packet is null or if no handler is found for the packet's command ID.

## Example Usage

Here's a basic example of how to configure and use the `PacketDispatcher` class:

```csharp
using Notio.Network.Handlers;
using Notio.Common.Logging;
using Notio.Common.Connection;

public class PacketDispatcherExample
{
    public void ConfigureDispatcher()
    {
        var logger = new ConsoleLogger();

        var options = new PacketDispatcherOptions()
            .WithLogging(logger)
            .WithPacketSerialization(
                packet => new Memory<byte>(packet.ToByteArray()), // Example serialization method
                data => new MyPacket(data.ToArray()) // Example deserialization method
            );

        var dispatcher = new PacketDispatcher(options);

        // Simulate handling a packet
        var connection = new MyConnection();
        byte[] packetData = { /* packet data */ };
        dispatcher.HandlePacket(packetData, connection);
    }
}
```

## Remarks

The `PacketDispatcher` class is designed to be flexible and extendable. It provides various methods to handle incoming packets, log errors and warnings, and invoke the appropriate handlers based on the packet command IDs.

Feel free to explore the individual methods and properties to understand their specific purposes and implementations. If you need detailed documentation for any specific file or directory, please refer to the source code or let me know!
