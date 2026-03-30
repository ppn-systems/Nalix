# Glossary

This page defines the core Nalix terms that appear repeatedly across the docs.

Use it as a quick refresher when the API pages start to feel too implementation-heavy.

## Protocol

`Protocol` is the bridge between a live connection and the dispatch pipeline.

In practice it:

- accepts or rejects new connections
- starts receive loops
- forwards incoming message frames into dispatch
- controls whether connections stay open after processing

## PacketContext

`PacketContext<TPacket>` is the per-request object passed to context-aware handlers and packet middleware.

It gives access to:

- the packet
- the current connection
- resolved packet metadata
- cancellation
- a pooled sender for manual replies

## Dispatch

In the docs, “dispatch” usually means `PacketDispatchChannel` plus its options/runtime.

This is the part that:

- queues incoming work
- deserializes packets
- runs middleware
- invokes handlers
- handles supported return types

## Connection

`Connection` is the runtime session object for one remote client.

It holds:

- connection ID
- remote endpoint
- TCP/UDP transport adapters
- permission level
- cipher/secret state
- runtime counters and events

## ConnectionHub

`ConnectionHub` is the in-memory registry of active connections.

Use it for:

- connection lookup
- username mapping
- forced disconnects
- bulk broadcast
- connection-level reporting

## TimingWheel

`TimingWheel` is the idle-timeout scheduler used by the network layer.

It helps detect and close dead or inactive connections efficiently.

## Middleware

Middleware is logic inserted before or around handler execution.

Nalix has two middleware layers:

- buffer middleware for raw frame processing
- packet middleware for packet-level policy and request flow

## Metadata Provider

A metadata provider is a component that adds extra metadata to handler methods while the runtime builds `PacketMetadata`.

Use it when attributes alone are not enough and you want conventions or custom policy tags.

## Return Handler

A return handler is the internal component that translates a handler's return type into a send action.

Examples:

- `TPacket`
- `Task<TPacket>`
- `string`
- `byte[]`
- `Memory<byte>`

## TCP vs UDP

- use **TCP** for reliable request/response and ordered traffic
- use **UDP** for low-latency datagrams where loss is acceptable and session/auth rules are handled separately

## Read this next

- [Choose the Right Building Block](./choose-the-right-building-block.md)
- [Architecture](./architecture.md)
- [Middleware](./middleware.md)
