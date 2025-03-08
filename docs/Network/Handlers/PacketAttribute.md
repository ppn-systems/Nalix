# Packet Attributes Documentation

This document provides details about the attributes used in the Notio.Network.Handlers namespace. These attributes define packet commands, their required authority levels, and mark classes as packet controllers.

## PacketCommandAttribute

The `PacketCommandAttribute` class is an attribute used to define a packet command and its required authority level. It is part of the `Notio.Network.Handlers` namespace.

### Namespace

```csharp
using Notio.Common.Models;
using System;
```

### Class Definition

#### Summary

The `PacketCommandAttribute` class allows you to specify a unique command identifier for a packet and the minimum authority level required to execute the command.

```csharp
namespace Notio.Network.Handlers
{
    /// <summary>
    /// Attribute to define a packet command and its required authority level.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class PacketCommandAttribute(ushort command, Authoritys authoritys = Authoritys.User) : Attribute
    {
        // Class implementation...
    }
}
```

### Properties

#### CommandId

```csharp
public ushort CommandId { get; } = command;
```

- **Description**: The unique command identifier for the packet.

#### RequiredAuthority

```csharp
public Authoritys RequiredAuthority { get; } = authoritys;
```

- **Description**: The minimum authority level required to execute this command.

### Example Usage

Here's a basic example of how to use the `PacketCommandAttribute` class:

```csharp
using Notio.Network.Handlers;

public class MyController
{
    [PacketCommand(1, Authoritys.Admin)]
    public void HandleAdminCommand(IPacket packet, IConnection connection)
    {
        // Handle admin command...
    }

    [PacketCommand(2, Authoritys.User)]
    public void HandleUserCommand(IPacket packet, IConnection connection)
    {
        // Handle user command...
    }
}
```

### Remarks

The `PacketCommandAttribute` class is designed to be used in conjunction with packet controllers to specify command IDs and required authority levels for handling packets.

## PacketControllerAttribute

The `PacketControllerAttribute` class is an attribute used to mark packet controllers responsible for handling packet commands. It is part of the `Notio.Network.Handlers` namespace.

### Namespace

```csharp
using System;
```

### Class Definition

#### Summary

The `PacketControllerAttribute` class marks a class as a packet controller, indicating that it contains methods for handling packet commands.

```csharp
namespace Notio.Network.Handlers
{
    /// <summary>
    /// Attribute used to mark packet controllers responsible for handling packet commands.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class PacketControllerAttribute : Attribute
    {
        // Class implementation...
    }
}
```

### Example Usage

Here's a basic example of how to use the `PacketControllerAttribute` class:

```csharp
using Notio.Network.Handlers;

[PacketController]
public class MyController
{
    [PacketCommand(1)]
    public void HandleCommand(IPacket packet, IConnection connection)
    {
        // Handle command...
    }
}
```

### Remarks

The `PacketControllerAttribute` class is designed to be used in conjunction with packet controllers to indicate that the class contains methods for handling packet commands.

Feel free to refer to the source code for more details on its implementation and usage.
