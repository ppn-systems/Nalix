# Packet Struct Documentation

The `Packet` struct represents a packet structure that can be pooled and disposed. This packet contains metadata such as the packet type, flags, priority, command, timestamp, and checksum, along with a payload containing the data for transmission. This struct is part of the `Notio.Network.Package` namespace.

## Namespace

```csharp
using Notio.Common.Exceptions;
using Notio.Common.Package;
using Notio.Cryptography.Integrity;
using Notio.Network.Package.Enums;
using Notio.Network.Package.Metadata;
using Notio.Network.Package.Utilities;
using Notio.Network.Package.Utilities.Data;
using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
```

## Struct Definition

### Summary

The `Packet` struct provides functionality for managing packet metadata and payload, including methods for initialization, validation, and memory management.

```csharp
namespace Notio.Network.Package
{
    /// <summary>
    /// Represents a packet structure that can be pooled and disposed.
    /// This packet contains metadata such as the packet type, flags, priority, command, timestamp, and checksum,
    /// along with a payload containing the data for transmission.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct Packet : IPacket
    {
        // Struct implementation...
    }
}
```

## Properties

### Length

```csharp
public readonly ushort Length => (ushort)(PacketSize.Header + Payload.Length);
```

- **Description**: Gets the total length of the packet including header and payload.

### Id

```csharp
public byte Id { get; }
```

- **Description**: Gets the packet identifier, which is a unique identifier for this packet instance.

### Type

```csharp
public byte Type { get; }
```

- **Description**: Gets the packet type, which specifies the kind of packet.

### Flags

```csharp
public byte Flags { get; }
```

- **Description**: Gets or sets the flags associated with the packet, used for additional control or state information.

### Priority

```csharp
public byte Priority { get; }
```

- **Description**: Gets the priority level of the packet, which can affect how the packet is processed or prioritized.

### Command

```csharp
public ushort Command { get; }
```

- **Description**: Gets the command associated with the packet, which can specify an operation or request type.

### Timestamp

```csharp
public ulong Timestamp { get; }
```

- **Description**: Gets the timestamp when the packet was created. This is a unique timestamp based on the system's current time.

### Checksum

```csharp
public uint Checksum { get; }
```

- **Description**: Gets or sets the checksum of the packet, computed based on the payload. Used for integrity validation.

### Payload

```csharp
public Memory<byte> Payload { get; }
```

- **Description**: Gets the payload of the packet, which contains the data being transmitted.

## Constructors

### Packet(ushort command, Memory<`byte`> payload)

```csharp
public Packet(ushort command, Memory<byte> payload)
    : this(PacketType.None, PacketFlags.None, PacketPriority.None, command, payload)
```

- **Description**: Initializes a new instance of the `Packet` struct with a specific command and payload.
- **Parameters**:
  - `command`: The packet command.
  - `payload`: The packet payload (data).

### Packet(byte type, byte flags, byte priority, ushort command, Memory<`byte`> payload)

```csharp
public Packet(byte type, byte flags, byte priority, ushort command, Memory<byte> payload)
    : this(null, type, flags, priority, command, null, null, payload)
```

- **Description**: Initializes a new instance of the `Packet` struct with type, flags, priority, command, and payload.
- **Parameters**:
  - `type`: The packet type.
  - `flags`: The packet flags.
  - `priority`: The packet priority.
  - `command`: The packet command.
  - `payload`: The packet payload (data).

### Packet(PacketType type, PacketFlags flags, PacketPriority priority, ushort command, Memory<`byte`> payload)

```csharp
public Packet(PacketType type, PacketFlags flags, PacketPriority priority, ushort command, Memory<byte> payload)
    : this((byte)type, (byte)flags, (byte)priority, command, payload)
```

- **Description**: Initializes a new instance of the `Packet` struct with specified packet metadata and payload.
- **Parameters**:
  - `type`: The packet type.
  - `flags`: The packet flags.
  - `priority`: The packet priority.
  - `command`: The packet command.
  - `payload`: The packet payload (data).

## Methods

### IsValid

```csharp
public readonly bool IsValid() => Crc32.HashToUInt32(Payload.Span) == Checksum;
```

- **Description**: Verifies the packet's checksum matches the computed checksum based on the payload.
- **Returns**: True if the checksum matches; otherwise, false.

### IsExpired

```csharp
public readonly bool IsExpired(TimeSpan timeout) =>
    (PacketTimeUtils.GetMicrosecondTimestamp() - Timestamp) > (ulong)timeout.TotalMilliseconds;
```

- **Description**: Determines if the packet has expired based on the provided timeout.
- **Parameters**:
  - `timeout`: The timeout to compare against the packet's timestamp.
- **Returns**: True if the packet has expired; otherwise, false.

### Equals(IPacket?)

```csharp
public readonly bool Equals(IPacket? other)
```

- **Description**: Compares the current packet with another packet for equality.
- **Parameters**:
  - `other`: The packet to compare with.
- **Returns**: True if the packets are equal; otherwise, false.

### Equals(object?)

```csharp
public override bool Equals(object? obj) => obj is Packet other && Equals(other);
```

- **Description**: Compares the current packet with another object for equality.
- **Parameters**:
  - `obj`: The object to compare with.
- **Returns**: True if the object is a `Packet` and is equal to the current packet; otherwise, false.

### GetHashCode

```csharp
public override int GetHashCode()
```

- **Description**: Returns a hash code for the current packet based on its fields and payload.
- **Returns**: A hash code for the current packet.

### Dispose

```csharp
public readonly void Dispose()
```

- **Description**: Releases the resources used by the packet. If the packet is pooled, it returns the memory to the ArrayPool.

### Operator Overloads

#### Equality

```csharp
public static bool operator ==(Packet left, Packet right) => left.Equals(right);
```

- **Description**: Determines whether two `Packet` objects are equal.
- **Parameters**:
  - `left`: The first `Packet` to compare.
  - `right`: The second `Packet` to compare.
- **Returns**: True if the two `Packet` objects are equal; otherwise, false.

#### Inequality

```csharp
public static bool operator !=(Packet left, Packet right) => !(left == right);
```

- **Description**: Determines whether two `Packet` objects are not equal.
- **Parameters**:
  - `left`: The first `Packet` to compare.
  - `right`: The second `Packet` to compare.
- **Returns**: True if the two `Packet` objects are not equal; otherwise, false.

## Example Usage

Here's a basic example of how to create and use the `Packet` struct:

```csharp
using Notio.Network.Package;
using System;

public class PacketExample
{
    public void CreateAndValidatePacket()
    {
        ushort command = 1;
        Memory<byte> payload = new byte[] { 0x01, 0x02, 0x03, 0x04 };

        Packet packet = new Packet(command, payload);

        if (packet.IsValid())
        {
            Console.WriteLine("Packet is valid.");
        }
        else
        {
            Console.WriteLine("Packet is invalid.");
        }
    }
}
```

## Remarks

The `Packet` struct is designed to manage packet metadata and payload efficiently, including methods for validation, expiration checking, and memory management. It supports various initialization options and provides methods for equality comparison and resource disposal.

Feel free to explore the individual methods and properties to understand their specific purposes and implementations. If you need detailed documentation for any specific file or directory, please refer to the source code or let me know!
