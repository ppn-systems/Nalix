# Selecting Building Blocks

If you are new to Nalix, this page helps you choose the right entry point quickly.

Use it when you know the problem you are solving, but not yet which Nalix piece should own it.

## Decision diagram

```mermaid
flowchart TD
    A["What are you trying to solve?"] --> B{"Need reliable ordered request/response?"}
    B -->|Yes| C["Use TCP: TcpListenerBase + Protocol + PacketDispatchChannel"]
    B -->|No| D{"Need low-latency datagrams?"}
    D -->|Yes| E["Use UDP: UdpListenerBase + session/auth flow"]
    D -->|No| F{"Need to change request behavior?"}
    F -->|Raw frame transformation| G["Use FramePipeline / Protocol"]
    F -->|After packet deserialization| H["Use MiddlewarePipeline"]
    F -->|Need custom policy tags or conventions| I["Use a metadata provider"]
```

## Decision Matrix

| You want to... | Use |
| :--- | :--- |
| Intercept, audit, or secure packets after deserialization | **MiddlewarePipeline** |
| Implement custom binary framing or binary security (AES, LZ4) | **Protocol / FramePipeline** |
| Customize how packets are queued, prioritized, or sharded | **Dispatch** |
| Implement business features or request logic | **Handler** |
| Tag handlers with region, tenant, or policy metadata | **Metadata Provider** |

## Quick rules

### Choose TCP when

- you want normal request/response
- ordering matters
- reliability matters
- you want the simplest starting path

Start with:

- `TcpListenerBase`
- `Protocol`
- `PacketDispatchChannel`

### Choose UDP when

- you want lower-latency datagrams
- you can tolerate packet loss
- you already understand session identity and auth requirements

Start with:

- `UdpListenerBase`
- a TCP-backed session/bootstrap path
- `IsAuthenticated(...)`

### Choose MiddlewarePipeline when

- the packet is already deserialized
- you need `PacketContext`
- you care about permission, timeout, rate limit, audit, or handler policy
- the handler may be a built-in packet or a custom packet type

### Choose Protocol / FramePipeline when

- you need to operate on raw bytes
- you want to decrypt, decompress, or validate frame integrity before deserialization

### Choose a metadata provider when

- you want custom attributes
- you want convention-based metadata
- you want middleware to read custom handler tags
- you want those tags to work consistently across built-in and custom packet handlers

## Common examples

| Need | Best fit |
| :--- | :--- |
| Public game login or command endpoint | TCP |
| Position/state update stream | UDP |
| Block unauthorized packets | MiddlewarePipeline |
| Decompress a frame before packet creation | FramePipeline / Protocol |
| Tag handlers by tenant/region/product | Metadata provider |

## Suggested first path for most clients

If you are unsure, choose this order:

1. TCP
2. standard packet attributes
3. packet middleware (`MiddlewarePipeline`)
4. metadata provider
5. UDP

That path is easier to debug and easier for teams to adopt.

## A safe default

If you do not have a strong reason to customize early, start with:

- TCP
- built-in packet attributes
- one small middleware registered in `MiddlewarePipeline`
- no custom metadata provider yet

That gives you the cleanest learning path and the fewest moving parts.

## Related pages

- [Glossary](../glossary.md)
- [Middleware](middleware-pipeline.md)
- [TCP Request/Response](../../guides/networking/tcp-patterns.md)
- [UDP Auth Flow](../../guides/networking/udp-security.md)
- [Custom Middleware](../../guides/extensibility/custom-middleware.md)
- [Custom Metadata Provider](../../guides/extensibility/metadata-providers.md)
