# IPacket Interface Documentation

The `IPacket` interface defines the contract for a network packet. It includes properties and methods for managing packet metadata and payload, ensuring data integrity, and handling packet expiration. This interface is part of the `Notio.Common.Package` namespace.

## Namespace

```csharp
using System;
```

## Interface Definition

### Summary

The `IPacket` interface provides a standardized way to handle network packets, including properties for packet metadata, payload, and methods for validation and expiration checks.

```csharp
namespace Notio.Common.Package
{
    /// <summary>
    /// Defines the contract for a network packet.
    /// </summary>
    public interface IPacket : IDisposable, IEquatable<IPacket>
    {
        // Interface definition...
    }
}
```

## Properties

### Length

```csharp
ushort Length { get; }
```

- **Description**: Gets the total length of the packet.

### Id

```csharp
byte Id { get; }
```

- **Description**: Gets the packet identifier.

### Type

```csharp
byte Type { get; }
```

- **Description**: Gets the packet type.

### Flags

```csharp
byte Flags { get; }
```

- **Description**: Gets or sets the packet flags.

### Priority

```csharp
byte Priority { get; }
```

- **Description**: Gets the packet priority.

### Command

```csharp
ushort Command { get; }
```

- **Description**: Gets the command associated with the packet.

### Timestamp

```csharp
ulong Timestamp { get; }
```

- **Description**: Gets the timestamp when the packet was created.

### Checksum

```csharp
uint Checksum { get; }
```

- **Description**: Gets the checksum of the packet.

### Payload

```csharp
Memory<byte> Payload { get; }
```

- **Description**: Gets the payload of the packet.

## Methods

### IsValid

```csharp
bool IsValid();
```

- **Description**: Verifies if the packet's checksum is valid.
- **Returns**: True if the checksum is valid; otherwise, false.

### IsExpired

```csharp
bool IsExpired(TimeSpan timeout);
```

- **Description**: Checks if the packet has expired based on the provided timeout.
- **Parameters**:
  - `timeout`: The expiration timeout.
- **Returns**: True if the packet has expired; otherwise, false.

## Example Usage

Here's a basic example of how to implement and use the `IPacket` interface:

```csharp
using System;
using Notio.Common.Package;

public class CustomPacket : IPacket
{
    public ushort Length => (ushort)(HeaderSize + Payload.Length);
    public byte Id { get; private set; }
    public byte Type { get; private set; }
    public byte Flags { get; set; }
    public byte Priority { get; private set; }
    public ushort Command { get; private set; }
    public ulong Timestamp { get; private set; }
    public uint Checksum { get; private set; }
    public Memory<byte> Payload { get; private set; }

    public CustomPacket(byte id, byte type, byte priority, ushort command, Memory<byte> payload)
    {
        Id = id;
        Type = type;
        Priority = priority;
        Command = command;
        Payload = payload;
        Timestamp = (ulong)DateTime.UtcNow.Ticks;
        Checksum = CalculateChecksum(payload);
    }

    public bool IsValid() => CalculateChecksum(Payload) == Checksum;

    public bool IsExpired(TimeSpan timeout) =>
        (ulong)(DateTime.UtcNow.Ticks - (long)Timestamp) > (ulong)timeout.Ticks;

    private uint CalculateChecksum(Memory<byte> data)
    {
        // Implement checksum calculation logic here
        return 0;
    }

    public void Dispose()
    {
        // Implement resource cleanup logic here
    }

    public bool Equals(IPacket other)
    {
        if (other == null) return false;
        return Id == other.Id && Type == other.Type && Command == other.Command;
    }

    public override bool Equals(object obj) => Equals(obj as IPacket);

    public override int GetHashCode() => HashCode.Combine(Id, Type, Command);
}
```

## Remarks

The `IPacket` interface is designed to standardize the handling of network packets in the Notio framework. It ensures that all packets have consistent metadata, payload management, and validation mechanisms.

Feel free to explore the properties and methods to understand their specific purposes and implementations. If you need detailed documentation for any specific file or directory, please refer to the source code or let me know!
