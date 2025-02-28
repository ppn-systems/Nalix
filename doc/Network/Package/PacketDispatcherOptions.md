# PacketDispatcherOptions Class Documentation

The `PacketDispatcherOptions` class provides configuration options for an instance of `PacketDispatcher`. It allows registering packet handlers, configuring logging, and defining error-handling strategies.

## Namespace

```csharp
using Notio.Common.Connection;
using Notio.Common.Exceptions;
using Notio.Common.Logging;
using Notio.Common.Package;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
```

## Class Definition

### Summary

The `PacketDispatcherOptions` class is designed to configure various aspects of the `PacketDispatcher`, including packet handling, logging, error handling, metrics tracking, packet compression, encryption, and serialization.

```csharp
namespace Notio.Network.Handlers
{
    /// <summary>
    /// Provides configuration options for an instance of <see cref="PacketDispatcher"/>.
    /// </summary>
    /// <remarks>
    /// This class allows registering packet handlers, configuring logging, and defining error-handling strategies.
    /// </remarks>
    public sealed class PacketDispatcherOptions
    {
        // Class implementation...
    }
}
```

## Properties

### Logger

```csharp
internal ILogger? Logger;
```

- **Description**: The logger instance used for logging.
- **Remarks**: If not configured, logging may be disabled.

### PacketHandlers

```csharp
internal readonly Dictionary<ushort, Func<IPacket, IConnection, Task>> PacketHandlers = new();
```

- **Description**: A dictionary mapping packet command IDs (ushort) to their respective handlers.

### ErrorHandler

```csharp
internal Action<Exception, ushort>? ErrorHandler;
```

- **Description**: Custom error handling strategy for packet processing.
- **Remarks**: If not set, the default behavior is to log errors.

### EnableMetrics

```csharp
private bool EnableMetrics { get; set; }
```

- **Description**: Indicates whether metrics tracking is enabled.

### MetricsCallback

```csharp
private Action<string, long>? MetricsCallback { get; set; }
```

- **Description**: Callback function to collect execution time metrics for packet processing.
- **Remarks**: The callback receives the packet handler name and execution time in milliseconds.

### SerializationMethod

```csharp
internal Func<IPacket, Memory<byte>>? SerializationMethod;
```

- **Description**: A function that serializes an `IPacket` into a `ReadOnlyMemory<byte>`.

### DeserializationMethod

```csharp
internal Func<ReadOnlyMemory<byte>, IPacket>? DeserializationMethod;
```

- **Description**: A function that deserializes a `Memory<byte>` into an `IPacket`.

## Constructors

### PacketDispatcherOptions()

```csharp
public PacketDispatcherOptions()
```

- **Description**: Initializes a new instance of the `PacketDispatcherOptions` class.
- **Remarks**: The constructor sets up the default packet handler methods and initializes the dictionary that stores the handlers for various return types. It also prepares fields for encryption, decryption, serialization, and compression methods, which can later be customized using the appropriate configuration methods.

## Methods

### WithMetrics

```csharp
public PacketDispatcherOptions WithMetrics(Action<string, long> metricsCallback)
```

- **Description**: Enables metrics tracking and sets the callback function for reporting execution times.
- **Parameters**:
  - `metricsCallback`: The callback function receiving the handler name and execution time in milliseconds.
- **Returns**: The current `PacketDispatcherOptions` instance for chaining.

### WithLogging

```csharp
public PacketDispatcherOptions WithLogging(ILogger logger)
```

- **Description**: Configures logging for the packet dispatcher.
- **Parameters**:
  - `logger`: The logger instance to use.
- **Returns**: The current `PacketDispatcherOptions` instance for chaining.

### WithErrorHandler

```csharp
public PacketDispatcherOptions WithErrorHandler(Action<Exception, ushort> errorHandler)
```

- **Description**: Configures a custom error handler for exceptions occurring during packet processing.
- **Parameters**:
  - `errorHandler`: The error handler action.
- **Returns**: The current `PacketDispatcherOptions` instance for chaining.

### WithHandler

```csharp
public PacketDispatcherOptions WithHandler<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods |
    DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TController>()
    where TController : new()
```

- **Description**: Registers a handler by scanning the specified controller type for methods decorated with `PacketCommandAttribute`.
- **Type Parameters**:
  - `TController`: The type of the controller to register.
- **Returns**: The current `PacketDispatcherOptions` instance for chaining.
- **Exceptions**:
  - `InvalidOperationException`: Thrown if a method with an unsupported return type is encountered or if duplicate command IDs are found.

### WithPacketCompression

```csharp
public PacketDispatcherOptions WithPacketCompression
(
    Func<IPacket, IPacket>? compressionMethod,
    Func<IPacket, IPacket>? decompressionMethod
)
```

- **Description**: Configures packet compression and decompression for the packet dispatcher.
- **Parameters**:
  - `compressionMethod`: A function that compresses a packet before sending. The function receives an `IPacket` and returns the compressed `IPacket`.
  - `decompressionMethod`: A function that decompresses a packet before processing. The function receives an `IPacket` and returns the decompressed `IPacket`.
- **Returns**: The current `PacketDispatcherOptions` instance for method chaining.
- **Remarks**: This method allows you to specify compression and decompression functions that will be applied to packets before they are sent or received.

### WithPacketCrypto

```csharp
public PacketDispatcherOptions WithPacketCrypto
(
    Func<IPacket, IConnection, IPacket>? encryptionMethod,
    Func<IPacket, IConnection, IPacket>? decryptionMethod
)
```

- **Description**: Configures packet encryption and decryption for the packet dispatcher.
- **Parameters**:
  - `encryptionMethod`: A function that encrypts a packet before sending. The function receives an `IPacket` and a byte array (encryption key), and returns the encrypted `IPacket`.
  - `decryptionMethod`: A function that decrypts a packet before processing. The function receives an `IPacket` and a byte array (decryption key), and returns the decrypted `IPacket`.
- **Returns**: The current `PacketDispatcherOptions` instance for method chaining.
- **Remarks**: This method allows you to specify encryption and decryption functions that will be applied to packets before they are sent or received.

### WithPacketSerialization

```csharp
public PacketDispatcherOptions WithPacketSerialization
(
    Func<IPacket, Memory<byte>>? serializationMethod,
    Func<ReadOnlyMemory<byte>, IPacket>? deserializationMethod
)
```

- **Description**: Configures the packet serialization and deserialization methods.
- **Parameters**:
  - `serializationMethod`: A function that serializes a packet into a `Memory<byte>`.
  - `deserializationMethod`: A function that deserializes a `Memory<byte>` back into an `IPacket`.
- **Returns**: The current `PacketDispatcherOptions` instance for method chaining.
- **Remarks**: This method allows customizing how packets are serialized before sending and deserialized upon receiving.

## Example Usage

Here's a basic example of how to configure the `PacketDispatcherOptions` class:

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
            .WithMetrics((handlerName, executionTime) =>
            {
                logger.Info($"{handlerName} executed in {executionTime}ms");
            })
            .WithErrorHandler((exception, commandId) =>
            {
                logger.Error($"Error processing command {commandId}: {exception.Message}");
            });

        // Configure additional options as needed...
    }
}
```

## Remarks

The `PacketDispatcherOptions` class is designed to be flexible and extendable. It provides various methods to configure packet handling, logging, error handling, metrics tracking, packet compression, encryption, and serialization.

Feel free to explore the individual methods and properties to understand their specific purposes and implementations. If you need detailed documentation for any specific file or directory, please refer to the source code or let me know!
