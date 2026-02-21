# Nalix.Common

`Nalix.Common` contains the shared contracts, packet metadata primitives, middleware contracts, and common enums used across the Nalix stack.

## Install

```bash
dotnet add package Nalix.Common
```

## What it includes

- Core contracts such as `IPacket` and `IConnection`
- Packet attributes including `PacketControllerAttribute` and `PacketOpcodeAttribute`
- Shared middleware abstractions used by server and client layers
- Common security and transport enums for consistent behavior across packages

## Typical use

Add this package when you need shared packet definitions, metadata attributes, or common transport contracts that must be referenced by both `Nalix.Network` and `Nalix.SDK`.

## Documentation

- Package docs: [Nalix.Common](https://ppn-systems.github.io/Nalix/packages/nalix-common/)
- API docs: [Common API](https://ppn-systems.github.io/Nalix/api/common/packet-contracts/)
