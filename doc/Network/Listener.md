# Listener Class Documentation

The `Listener` class is an abstract base class for network listeners. It manages the process of accepting incoming network connections and handling the associated protocol processing. This class is part of the `Notio.Network.Listeners` namespace and is designed to facilitate efficient communication and data sharing through real-time server solutions.

## Namespace

```csharp
using Notio.Common.Connection;
using Notio.Common.Logging;
using Notio.Common.Memory;
using Notio.Network.Config;
using Notio.Network.Protocols;
using Notio.Shared.Configuration;
using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
```

## Class Definition

### Summary

The `Listener` class is designed to accept incoming network connections, process them using a specified protocol, and manage connection buffers. It includes support for configuration, logging, and high-performance socket operations.

```csharp
namespace Notio.Network.Listeners
{
    /// <summary>
    /// An abstract base class for network listeners.
    /// This class manages the process of accepting incoming network connections
    /// and handling the associated protocol processing.
    /// </summary>
    public abstract class Listener : TcpListener, IListener
    {
        // Class implementation...
    }
}
```

## Properties

### NetworkConfig

```csharp
private static readonly NetworkConfig NetworkConfig = ConfiguredShared.Instance.Get<NetworkConfig>();
```

- **Description**: Provides network configuration settings.

### _port

```csharp
private readonly int _port;
```

- **Description**: The port to listen on.

### _logger

```csharp
private readonly ILogger _logger;
```

- **Description**: The logger to log events and errors.

### _protocol

```csharp
private readonly IProtocol _protocol;
```

- **Description**: The protocol to handle the connections.

### _bufferPool

```csharp
private readonly IBufferPool _bufferPool;
```

- **Description**: The buffer pool for managing connection buffers.

### _listenerLock

```csharp
private readonly SemaphoreSlim _listenerLock = new(1, 1);
```

- **Description**: A semaphore to manage concurrent access to the listener.

## Constructors

### Listener(int port, IProtocol protocol, IBufferPool bufferPool, ILogger logger)

```csharp
protected Listener(int port, IProtocol protocol, IBufferPool bufferPool, ILogger logger)
    : base(IPAddress.Any, port)
```

- **Description**: Initializes a new instance of the `Listener` class with the specified port, protocol, buffer pool, and logger.
- **Parameters**:
  - `port`: The port to listen on.
  - `protocol`: The protocol to handle the connections.
  - `bufferPool`: The buffer pool for managing connection buffers.
  - `logger`: The logger to log events and errors.
- **Exceptions**:
  - `ArgumentNullException`: Thrown if `logger`, `protocol`, or `bufferPool` is null.

### Listener(IProtocol protocol, IBufferPool bufferPool, ILogger logger)

```csharp
protected Listener(IProtocol protocol, IBufferPool bufferPool, ILogger logger)
    : base(IPAddress.Any, NetworkConfig.Port)
```

- **Description**: Initializes a new instance of the `Listener` class using the port defined in the configuration and the specified protocol, buffer pool, and logger.
- **Parameters**:
  - `protocol`: The protocol to handle the connections.
  - `bufferPool`: The buffer pool for managing connection buffers.
  - `logger`: The logger to log events and errors.
- **Exceptions**:
  - `ArgumentNullException`: Thrown if `logger`, `protocol`, or `bufferPool` is null.

## Methods

### BeginListening

```csharp
public void BeginListening(CancellationToken cancellationToken)
```

- **Description**: Starts listening for incoming connections and processes them using the specified protocol. The listening process can be cancelled using the provided `CancellationToken`.
- **Parameters**:
  - `cancellationToken`: A `CancellationToken` to cancel the listening process.

### EndListening

```csharp
public void EndListening()
```

- **Description**: Stops the listener from accepting further connections.

### CreateConnection

```csharp
private async Task<IConnection> CreateConnection(CancellationToken cancellationToken)
```

- **Description**: Creates a new connection from an incoming socket.
- **Parameters**:
  - `cancellationToken`: The cancellation token for the connection creation process.
- **Returns**: A task representing the connection creation.

### OnConnectionClose

```csharp
private void OnConnectionClose(object? sender, IConnectEventArgs args)
```

- **Description**: Handles the closure of a connection by unsubscribing from its events.
- **Parameters**:
  - `sender`: The source of the event.
  - `args`: The connection event arguments.

### ConfigureHighPerformanceSocket

```csharp
private void ConfigureHighPerformanceSocket(Socket socket)
```

- **Description**: Configures the socket for high-performance operation by setting buffer sizes, timeouts, and keep-alive options.
- **Parameters**:
  - `socket`: The socket to configure.
- **Remarks**: Keep-alive settings are only applied on Windows platforms.

### GetKeepAliveValues

```csharp
private static byte[] GetKeepAliveValues()
```

- **Description**: Gets the keep-alive values for Windows sockets.
- **Returns**: A byte array containing the keep-alive values.

## Example Usage

Here's a basic example of how to use the `Listener` class:

```csharp
using Notio.Common.Connection;
using Notio.Common.Logging;
using Notio.Common.Memory;
using Notio.Network.Protocols;
using Notio.Network.Listeners;

public class ExampleListener : Listener
{
    public ExampleListener(IProtocol protocol, IBufferPool bufferPool, ILogger logger)
        : base(protocol, bufferPool, logger)
    {
    }
}

public class ExampleUsage
{
    public void StartListener()
    {
        var protocol = new ExampleProtocol();
        var bufferPool = new BufferPool();
        var logger = new ConsoleLogger();

        var listener = new ExampleListener(protocol, bufferPool, logger);
        listener.BeginListening(CancellationToken.None);
    }
}
```

## Remarks

The `Listener` class is designed to be easily configurable and extendable. It supports high-performance socket operations and can handle various protocols through its `IProtocol` interface.

Feel free to explore the individual methods and properties to understand their specific purposes and implementations. If you need detailed documentation for any specific file or directory, please refer to the source code or let me know!
