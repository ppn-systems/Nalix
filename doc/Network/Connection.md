# Connection Class Documentation

The `Connection` class represents a network connection that manages socket communication, stream transformation, and event handling. This class is part of the `Notio.Network.Connection` namespace.

## Namespace

```csharp
using Notio.Common.Connection;
using Notio.Common.Connection.Enums;
using Notio.Common.Cryptography;
using Notio.Common.Logging;
using Notio.Common.Memory;
using Notio.Common.Models;
using Notio.Identification;
using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
```

## Class Definition

### Summary

The `Connection` class provides functionality for managing network connections, including sending and receiving data, handling encryption, and managing connection states.

```csharp
namespace Notio.Network.Connection
{
    /// <summary>
    /// Represents a network connection that manages socket communication, stream transformation, and event handling.
    /// </summary>
    public sealed class Connection : IConnection
    {
        // Class implementation...
    }
}
```

## Properties

### Id

```csharp
public string Id => _id.ToString(true);
```

- **Description**: Gets the unique identifier for the connection.

### PingTime

```csharp
public long PingTime => _cstream.LastPingTime;
```

- **Description**: Gets the last ping time for the connection.

### EncryptionKey

```csharp
public byte[] EncryptionKey
{
    get => _encryptionKey;
    set
    {
        if (value is null || value.Length != 32)
            throw new ArgumentException("EncryptionKey must be exactly 16 bytes.", nameof(value));

        lock (_lock)
        {
            _encryptionKey = value;
        }
    }
}
```

- **Description**: Gets or sets the encryption key for the connection.

### RemoteEndPoint

```csharp
public string RemoteEndPoint
{
    get
    {
        if (_remoteEndPoint == null && _socket.Connected)
            _remoteEndPoint = _socket.RemoteEndPoint?.ToString() ?? "0.0.0.0";

        return _remoteEndPoint ?? "Disconnected";
    }
}
```

- **Description**: Gets the remote endpoint of the connection.

### IncomingPacket

```csharp
public ReadOnlyMemory<byte> IncomingPacket
{
    get
    {
        if (_cstream.CacheIncoming.TryGetValue(out ReadOnlyMemory<byte> data))
            return data;

        return ReadOnlyMemory<byte>.Empty;
    }
}
```

- **Description**: Gets the incoming packet data.

### Authority

```csharp
public Authoritys Authority { get; set; } = Authoritys.Guests;
```

- **Description**: Gets or sets the authority level for the connection.

### Mode

```csharp
public EncryptionMode Mode { get; set; } = EncryptionMode.Xtea;
```

- **Description**: Gets or sets the encryption mode for the connection.

### Timestamp

```csharp
public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
```

- **Description**: Gets the timestamp when the connection was created.

### State

```csharp
public ConnectionState State { get; set; } = ConnectionState.Connected;
```

- **Description**: Gets or sets the state of the connection.

## Events

### OnCloseEvent

```csharp
public event EventHandler<IConnectEventArgs>? OnCloseEvent
{
    add => _onCloseEvent += value;
    remove => _onCloseEvent -= value;
}
```

- **Description**: Occurs when the connection is closed.

### OnProcessEvent

```csharp
public event EventHandler<IConnectEventArgs>? OnProcessEvent
{
    add => _onProcessEvent += value;
    remove => _onProcessEvent -= value;
}
```

- **Description**: Occurs when a packet is processed.

### OnPostProcessEvent

```csharp
public event EventHandler<IConnectEventArgs>? OnPostProcessEvent
{
    add => _onPostProcessEvent += value;
    remove => _onPostProcessEvent -= value;
}
```

- **Description**: Occurs after a packet is processed.

## Methods

### BeginReceive

```csharp
public void BeginReceive(CancellationToken cancellationToken = default)
```

- **Description**: Begins receiving data on the connection.
- **Parameters**:
  - `cancellationToken`: A `CancellationToken` to cancel the receive operation.

### Send

```csharp
public bool Send(Memory<byte> message)
```

- **Description**: Sends a message synchronously.
- **Parameters**:
  - `message`: The message to send.
- **Returns**: `true` if the message was sent successfully; otherwise, `false`.

### SendAsync

```csharp
public async Task<bool> SendAsync(Memory<byte> message, CancellationToken cancellationToken = default)
```

- **Description**: Sends a message asynchronously.
- **Parameters**:
  - `message`: The message to send.
  - `cancellationToken`: A `CancellationToken` to cancel the send operation.
- **Returns**: A `Task` representing the asynchronous operation. The task result contains `true` if the message was sent successfully; otherwise, `false`.

### Close

```csharp
public void Close(bool force = false)
```

- **Description**: Closes the connection.
- **Parameters**:
  - `force`: If `true`, forces the connection to close immediately.

### Disconnect

```csharp
public void Disconnect(string? reason = null)
```

- **Description**: Disconnects the connection.
- **Parameters**:
  - `reason`: The reason for the disconnection.

### Dispose

```csharp
public void Dispose()
```

- **Description**: Releases all resources used by the `Connection` instance.

## Example Usage

Here's a basic example of how to use the `Connection` class:

```csharp
using System;
using System.Net;
using System.Net.Sockets;
using Notio.Common.Connection;
using Notio.Common.Logging;
using Notio.Common.Memory;
using Notio.Network.Connection;

public class ConnectionExample
{
    public void CreateConnection()
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        var bufferPool = new BufferPool();
        var logger = new ConsoleLogger();

        var connection = new Connection(socket, bufferPool, logger);

        connection.OnCloseEvent += (sender, args) =>
        {
            Console.WriteLine("Connection closed.");
        };

        connection.OnProcessEvent += (sender, args) =>
        {
            Console.WriteLine("Packet processed.");
        };

        connection.OnPostProcessEvent += (sender, args) =>
        {
            Console.WriteLine("Post-processing completed.");
        };

        connection.BeginReceive();
    }
}
```

## Remarks

The `Connection` class is designed to manage network connections efficiently, providing features such as encryption, asynchronous communication, and event handling. It is suitable for applications requiring robust and secure network communication.

Feel free to explore the individual methods and properties to understand their specific purposes and implementations. If you need detailed documentation for any specific file or directory, please refer to the source code or let me know!
