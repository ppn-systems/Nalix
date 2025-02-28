# IConnection Interface Documentation

The `IConnection` interface represents a contract for managing a network connection. It includes properties and methods for handling connection metadata, encryption, event handling, and data transmission. This interface is part of the `Notio.Common.Connection` namespace.

## Namespace

```csharp
using Notio.Common.Connection.Enums;
using Notio.Common.Cryptography;
using Notio.Common.Models;
using System;
using System.Threading;
using System.Threading.Tasks;
```

## Interface Definition

### Summary

The `IConnection` interface provides a standardized way to manage network connections, including properties for connection metadata, encryption, and methods for data transmission and event handling.

```csharp
namespace Notio.Common.Connection
{
    /// <summary>
    /// Represents an interface for managing a network connection.
    /// </summary>
    public interface IConnection : IDisposable
    {
        // Interface definition...
    }
}
```

## Properties

### Id

```csharp
string Id { get; }
```

- **Description**: Gets the unique identifier for the connection.

### PingTime

```csharp
long PingTime { get; }
```

- **Description**: Gets the ping time (round-trip time) for the connection, measured in milliseconds. This value can help determine the latency of the network connection.

### IncomingPacket

```csharp
ReadOnlyMemory<byte> IncomingPacket { get; }
```

- **Description**: Gets the incoming packet of data.

### RemoteEndPoint

```csharp
string RemoteEndPoint { get; }
```

- **Description**: Gets the remote endpoint address associated with the connection.

### Timestamp

```csharp
DateTimeOffset Timestamp { get; }
```

- **Description**: Gets the timestamp indicating when the connection was established.

### EncryptionKey

```csharp
byte[] EncryptionKey { get; set; }
```

- **Description**: Gets the encryption key used for securing communication.

### Mode

```csharp
EncryptionMode Mode { get; set; }
```

- **Description**: Gets or sets the encryption mode used.

### Authority

```csharp
Authoritys Authority { get; set; }
```

- **Description**: Gets the authority levels associated with the connection.

### State

```csharp
ConnectionState State { get; set; }
```

- **Description**: Gets the current state of the connection.

## Events

### OnCloseEvent

```csharp
event EventHandler<IConnectEventArgs> OnCloseEvent;
```

- **Description**: Occurs when the connection is closed.

### OnProcessEvent

```csharp
event EventHandler<IConnectEventArgs> OnProcessEvent;
```

- **Description**: Occurs when data is received and processed.

### OnPostProcessEvent

```csharp
event EventHandler<IConnectEventArgs> OnPostProcessEvent;
```

- **Description**: Occurs after data has been successfully processed.

## Methods

### BeginReceive

```csharp
void BeginReceive(CancellationToken cancellationToken = default);
```

- **Description**: Starts receiving data from the connection.
- **Parameters**:
  - `cancellationToken`: A token to cancel the receiving operation. (optional)

### Close

```csharp
void Close(bool force = false);
```

- **Description**: Closes the connection and releases all associated resources.
- **Parameters**:
  - `force`: Whether to forcefully close the connection. (optional)

### Send

```csharp
bool Send(Memory<byte> message);
```

- **Description**: Sends a message synchronously over the connection.
- **Parameters**:
  - `message`: The message to send.
- **Returns**: True if the message was sent successfully; otherwise, false.

### SendAsync

```csharp
Task<bool> SendAsync(Memory<byte> message, CancellationToken cancellationToken = default);
```

- **Description**: Sends a message asynchronously over the connection.
- **Parameters**:
  - `message`: The data to send.
  - `cancellationToken`: A token to cancel the sending operation. (optional)
- **Returns**: A task that represents the asynchronous sending operation.

### Disconnect

```csharp
void Disconnect(string reason = null);
```

- **Description**: Disconnects the connection safely with an optional reason.
- **Parameters**:
  - `reason`: An optional string providing the reason for disconnection.

## Example Usage

Here's a basic example of how to implement and use the `IConnection` interface:

```csharp
using Notio.Common.Connection;
using Notio.Common.Connection.Enums;
using Notio.Common.Cryptography;
using System;
using System.Threading;
using System.Threading.Tasks;

public class NetworkConnection : IConnection
{
    public string Id { get; private set; }
    public long PingTime { get; private set; }
    public ReadOnlyMemory<byte> IncomingPacket { get; private set; }
    public string RemoteEndPoint { get; private set; }
    public DateTimeOffset Timestamp { get; private set; }
    public byte[] EncryptionKey { get; set; }
    public EncryptionMode Mode { get; set; }
    public Authoritys Authority { get; set; }
    public ConnectionState State { get; set; }

    public event EventHandler<IConnectEventArgs> OnCloseEvent;
    public event EventHandler<IConnectEventArgs> OnProcessEvent;
    public event EventHandler<IConnectEventArgs> OnPostProcessEvent;

    public NetworkConnection(string id)
    {
        Id = id;
        Timestamp = DateTimeOffset.UtcNow;
    }

    public void BeginReceive(CancellationToken cancellationToken = default)
    {
        // Implementation for receiving data
    }

    public void Close(bool force = false)
    {
        // Implementation for closing the connection
        OnCloseEvent?.Invoke(this, new ConnectEventArgs { Message = "Connection closed." });
    }

    public bool Send(Memory<byte> message)
    {
        // Implementation for sending data synchronously
        return true;
    }

    public async Task<bool> SendAsync(Memory<byte> message, CancellationToken cancellationToken = default)
    {
        // Implementation for sending data asynchronously
        return await Task.FromResult(true);
    }

    public void Disconnect(string reason = null)
    {
        // Implementation for disconnecting the connection
        OnCloseEvent?.Invoke(this, new ConnectEventArgs { Message = reason ?? "Disconnected." });
    }

    public void Dispose()
    {
        // Clean up resources
    }

    public bool Equals(IConnection other)
    {
        return other != null && Id == other.Id;
    }

    public override bool Equals(object obj) => Equals(obj as IConnection);

    public override int GetHashCode() => Id.GetHashCode();
}
```

## Remarks

The `IConnection` interface is designed to standardize the management of network connections in the Notio framework. It ensures that all connections have consistent metadata, encryption handling, event management, and data transmission mechanisms.

Feel free to explore the properties, events, and methods to understand their specific purposes and implementations. If you need detailed documentation for any specific file or directory, please refer to the source code or let me know!
