# Middleware

This page explains how middleware fits into the Nalix request path.

Use it when you want to decide where custom logic belongs before you start writing handlers.

## The simple model

Nalix has two middleware layers:

- buffer middleware for raw frames before packet deserialization
- packet middleware for policy and flow around handler execution

That split matters more than any individual middleware type. Most mistakes come from choosing the wrong layer first.

```mermaid
flowchart LR
    A["Socket frame"] --> B["Buffer middleware"]
    B --> C["Deserialize packet"]
    C --> D["Packet middleware"]
    D --> E["Handler"]
    E --> F["Return handler / reply"]
```

## Buffer middleware

Buffer middleware runs before a packet exists.

Use it when you need to work with raw bytes:

- decrypt or decompress a frame
- reject invalid or suspicious frame shapes early
- perform low-level protocol checks
- stop bad traffic before packet allocation and handler lookup

The important tradeoff is simple:

- you get early control
- you do not get `PacketContext`

## Packet middleware

Packet middleware runs after deserialization.

Use it when you need application-aware behavior:

- permissions
- timeout rules
- rate limits
- concurrency limits
- auditing
- tenant or product policy checks

This is where `PacketContext<TPacket>` and resolved metadata become useful.

## How metadata fits in

Middleware becomes powerful because dispatch resolves metadata before packet middleware runs.

That means packet middleware can read:

- `PacketOpcode`
- permission rules
- timeout rules
- rate-limit rules
- concurrency rules
- custom attributes added by metadata providers

So the usual flow is:

1. declare attributes on handlers
2. optionally enrich them with `IPacketMetadataProvider`
3. read the resolved metadata inside middleware

## How to choose the right layer

Choose buffer middleware when:

- the packet is not safe to deserialize yet
- the decision depends on bytes, framing, crypto, or compression

Choose packet middleware when:

- the request is already a packet
- the decision depends on handler metadata, permissions, or connection state

If you are unsure, start with packet middleware. It is easier to test, easier to debug, and usually the right layer for app-level policy.

## Example decisions

| Need | Best fit |
|---|---|
| Reject a malformed frame before packet creation | Buffer middleware |
| Decrypt a wrapped payload | Buffer middleware |
| Block a packet by permission level | Packet middleware |
| Apply per-handler timeout rules | Packet middleware |
| Read a custom tenant tag from metadata | Packet middleware |

## Minimal example

```csharp
PacketDispatchChannel dispatch = new(options =>
{
    options.NetworkPipeline.Use(new SampleAuditBufferMiddleware());
    options.PacketPipeline.Use(new SampleAuditMiddleware<IPacket>());
});
```

In that example:

- `NetworkPipeline` handles raw-frame work
- `PacketPipeline` handles packet-aware policy

## Common advice

- keep buffer middleware narrow and cheap
- keep packet middleware policy-focused
- use metadata providers for conventions, not runtime decisions
- short-circuit early when the request should not continue
- prefer one clear middleware per concern over one giant policy class

## Read this next

- [Choose the Right Building Block](./choose-the-right-building-block.md)
- [Middleware Pipeline](../api/runtime/middleware/pipeline.md)
- [Packet Metadata](../api/runtime/routing/packet-metadata.md)
- [Custom Middleware End-to-End](../guides/custom-middleware-end-to-end.md)
