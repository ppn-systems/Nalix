# Protocol Class Documentation

The `Protocol` class is an abstract base class for network protocols. It defines the common logic for handling connections and processing messages. This class is part of the `Notio.Network.Protocols` namespace and is designed to be extended by specific protocol implementations.

## Namespace

```csharp
using Notio.Common.Connection;
```

## Class Definition

### Summary

The `Protocol` class provides a foundation for network protocols, including methods for accepting connections, processing messages, and handling post-processing logic.

```csharp
namespace Notio.Network.Protocols
{
    /// <summary>
    /// Represents an abstract base class for network protocols.
    /// This class defines the common logic for handling connections and processing messages.
    /// </summary>
    public abstract class Protocol : IProtocol
    {
        // Class implementation...
    }
}
```

## Properties

### KeepConnectionOpen

```csharp
private bool _keepConnectionOpen = false;

public virtual bool KeepConnectionOpen
{
    get => _keepConnectionOpen;
    protected set => _keepConnectionOpen = value;
}
```

- **Description**: Gets or sets a value indicating whether the connection should be kept open after processing. Default value is `false` unless overridden.

## Methods

### OnAccept

```csharp
public virtual void OnAccept(IConnection connection)
```

- **Description**: Called when a connection is accepted. Starts receiving data by default. Override to implement custom acceptance logic, such as IP validation.
- **Parameters**:
  - `connection`: The connection to be processed.

### PostProcessMessage

```csharp
public void PostProcessMessage(object sender, IConnectEventArgs args)
```

- **Description**: Post-processes a message after it has been handled. If the connection should not remain open, it will be disconnected.
- **Parameters**:
  - `sender`: The sender of the event.
  - `args`: Event arguments containing the connection and additional data.

### OnPostProcess

```csharp
protected virtual void OnPostProcess(IConnectEventArgs args)
```

- **Description**: Allows subclasses to execute custom logic after a message has been processed. This method is called automatically by `PostProcessMessage`.
- **Parameters**:
  - `args`: Event arguments containing connection and processing details.

### ProcessMessage

```csharp
public abstract void ProcessMessage(object sender, IConnectEventArgs args);
```

- **Description**: Processes a message received on the connection. This method must be implemented by derived classes to handle specific message processing.
- **Parameters**:
  - `sender`: The sender of the message.
  - `args`: Event arguments containing the connection and message data.

## Example Usage

Here's a basic example of how to extend the `Protocol` class:

```csharp
using Notio.Common.Connection;
using Notio.Network.Protocols;

public class CustomProtocol : Protocol
{
    public override void ProcessMessage(object sender, IConnectEventArgs args)
    {
        // Custom message processing logic
    }

    protected override void OnPostProcess(IConnectEventArgs args)
    {
        // Custom post-processing logic
    }
}
```

## Remarks

The `Protocol` class is designed to be extended by specific protocol implementations. It provides a flexible foundation for handling network connections and processing messages.

Feel free to explore the individual methods and properties to understand their specific purposes and implementations. If you need detailed documentation for any specific file or directory, please refer to the source code or let me know!
