# Nalix.Runtime.Handlers

This namespace contains the logic for finding and invoking classes marked as packet handlers.

## Core Concepts

### [Packet Controllers](../routing/packet-attributes.md)
Classes decorated with `[PacketController]` are automatically discovered by the `NetworkApplicationBuilder`.

### [Handler Methods](../routing/packet-attributes.md)
Individual methods decorated with `[PacketOpcode]` define the logic for a specific message type.

## Supported Signatures

Handlers can take several forms:
1. `public TPacketResponse Handle(TPacketRequest request)`
2. `public ValueTask<TPacketResponse> Handle(IPacketContext<TPacketRequest> context)`
3. `public async Task Handle(IPacketContext<TPacketRequest> context, CancellationToken ct)`

For more details on return types, see [Handler Result Types](../routing/handler-results.md).
