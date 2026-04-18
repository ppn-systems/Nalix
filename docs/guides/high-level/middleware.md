# Middleware Guide

!!! info "Learning Signals"
    - :fontawesome-solid-layer-group: **Level**: Intermediate
    - :fontawesome-solid-clock: **Time**: 10–15 minutes
    - :fontawesome-solid-book: **Prerequisites**: [Quickstart](../quickstart.md)

Use this guide when you know you need request policy or frame processing, but you are not yet sure which Nalix middleware layer should own it.

## Start with the right question

Before you write middleware, ask:

- do I need raw bytes, or a deserialized packet?
- am I enforcing transport safety, or application policy?
- should this behavior live in one middleware, or become handler metadata?

Those questions usually point you to the right shape before code does.

## The two middleware layers

Nalix has two middleware paths:

- buffer middleware before deserialization
- packet middleware after deserialization

```mermaid
flowchart LR
    A["Inbound frame"] --> B["Buffer middleware"]
    B --> C["Deserialize packet"]
    C --> D["Packet middleware"]
    D --> E["Handler"]
```

## Choose buffer middleware when

Use buffer middleware when the request is not safe to deserialize yet.

Typical cases:

- decrypting wrapped frames
- decompressing payloads
- validating frame shape
- dropping malformed or obviously abusive traffic early

This layer is early and cheap, but it does not know handler metadata yet.

## Choose packet middleware when

Use packet middleware when the request is already a packet and the decision depends on application state.

Typical cases:

- permission checks
- timeout rules
- rate limiting
- concurrency limits
- tenant or region policy
- auditing and tracing

For most teams, this is the easiest place to start.

## A safe build order

For most projects, middleware usually grows cleanly in this order:

1. start with one packet middleware
2. add metadata-driven policy only when repeated rules appear
3. add buffer middleware only when raw-frame handling is truly needed

That path keeps debugging simple and avoids adding transport complexity too early.

## Example: one packet middleware

```csharp
PacketDispatchChannel dispatch = new(options =>
{
    options.WithLogging(logger)
           .WithMiddleware(new SampleAuditMiddleware<IPacket>())
           .WithHandler(() => new SamplePingHandlers());
});
```

Even one middleware can already:

- log opcode and endpoint
- enforce permission rules
- short-circuit bad requests

## Example: one buffer middleware

```csharp
options.WithBufferMiddleware(new SampleAuditBufferMiddleware());
```

Keep buffer middleware narrow. If the logic starts depending on handler attributes or application roles, it probably belongs in packet middleware instead.

## Common mistakes

- using buffer middleware for app-level permission checks
- putting too many unrelated policies in one middleware
- adding custom metadata before proving you need it
- forgetting that middleware order changes behavior

## Good default patterns

For public traffic, a good starting setup is:

- `ConnectionGuard` at accept time
- one packet middleware for permission or audit policy
- built-in rate or concurrency controls where needed

For internal traffic, you can stay lighter:

- keep middleware minimal
- add metadata only for repeated conventions
- prefer simple handler returns over manual send flow

## Read this next

- [Custom Middleware End-to-End](./custom-middleware-end-to-end.md)
- [Custom Metadata Provider](./custom-metadata-provider.md)
- [Middleware](../concepts/middleware.md)
- [Middleware Pipeline](../api/runtime/middleware/pipeline.md)
