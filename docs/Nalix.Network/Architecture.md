# Nalix.Network – Detailed Architecture

## 1. Introduction

**Nalix.Network** is a modern, extensible .NET networking library for building high-performance TCP/UDP servers.  
It offers a robust, scalable pipeline for packet dispatch, resource management, and protocol layering—allowing applications to add domain logic, handling, and security transparently through middleware and DI-based handlers.

This document provides a detailed look at the system architecture, code layering, middleware flow, and dispatch mechanisms of Nalix.Network.

---

## 2. System and Layer Architecture

### 2.1 High-Level System View

Nalix.Network is a library you embed in your server application/process for direct network transport and protocol handling.

```mermaid
flowchart TD
    subgraph CLIENT
      direction TB
      A[Frontend Application<br/>(Mobile, Desktop, IoT, Game...)]
      B[System (Other Service)]
    end

    subgraph "NALIX.NETWORK TCP LIBRARY"
      direction TB
      Listeners[Tcp/Udp Listener]
      Protocols[Protocol & Routing]
      Middleware[Middleware Pipeline<br/>(Inbound/Outbound)]
      Dispatch[Packet Dispatcher/Router]
      Domain[Domain Logic<br/>(Packet, Handler, Context)]
      ConnectionMgmt[Connection Management & Pool]
      Infra[Transport Layer<br/>(FramedSocketChannel, TimingWheel, Pool)]
    end

    subgraph INFRA
      Config[App settings\n& configuration]
      Logging[Logging/error]
      Tasks[Worker Tasks/thread pool]
      [Object Pool]
    end

    A & B -- TCP/UDP --> Listeners
    Listeners --> Protocols
    Protocols --> Middleware
    Middleware --> Dispatch
    Dispatch --> Domain
    Domain <---> ConnectionMgmt
    ConnectionMgmt <--> Infra
    Infra <--> INFRA

    classDef inner fill:#f6fafd,stroke:#4682b4,stroke-width:1px;
    class Listeners,Protocols,Middleware,Dispatch,Domain,ConnectionMgmt,Infra inner;
```

---

### 2.2 Layered Code Structure (DDD/Clean Architecture)

#### Layer Mapping

| Layer             | Key Namespaces/Types                                                                | Function                                 |
|-------------------|-------------------------------------------------------------------------------------|------------------------------------------|
| **Application**   | Listeners, Protocols, Handler Controllers                                           | Orchestrates workflows, app entry, glue  |
| **Domain**        | Core entities: Packet, Context, Connection, MiddlewarePipeline, Handler descriptors | Business/connection logic, main contracts|
| **Infrastructure**| Internal.Transport, Pool, TimingWheel, Configuration                                | Low-level I/O, resource, timer, DI, pool |
| **Shared**        | Interfaces, Enums, MemoryUtils                                                      | Used/referenced cross-layers             |

#### Diagram

```mermaid
flowchart TD
    subgraph Application
      Listener[TcpListenerBase/UdpListener]
      Protocol[ProtocolBase]
      Handler[Packet Handlers/Controllers]
    end
    subgraph Domain
      PacketCtx[PacketContext]
      Connection[Connection (IConnection)]
      Pipeline[MiddlewarePipeline]
      Router[Packet Dispatch/Router/Registry]
    end
    subgraph Infra
      FramedSocket[FramedSocketChannel]
      Timing[TimingWheel/TimeSync]
      Pool[ObjectPool]
      Config[Configuration]
      Logger[Logging]
    end
    subgraph Shared
      IPacket[IPacket/IConnection/IHandler/Enums]
      Buffer[Buffer/MemUtils]
    end

    Listener --> Protocol
    Protocol -.-> Pipeline
    Pipeline --> Router
    Router -->|Dispatch| Handler
    Handler -.-> PacketCtx
    PacketCtx --> Connection
    Pipeline --> Connection
    Connection -.-> FramedSocket
    FramedSocket -.-> Pool
    FramedSocket -.-> Logger
    Listener -.-> Timing
    Infra -.-> Config
    Shared -.-> Infra
    Shared -.-> Domain
```

---

## 3. Core Flows: Packet Lifecycle

### 3.1 High-Speed Packet Dispatch with Queue Routing

The heart of packet processing is the **DispatchChannel**, **Router**, and **PacketDispatchChannel** classes:

```mermaid
sequenceDiagram
    participant Client as Client (TCP/UDP)
    participant TcpListener as TcpListener
    participant Protocol as Protocol
    participant Connection as Connection
    participant Dispatch as PacketDispatchChannel
    participant Middleware as MiddlewarePipeline/IPacketMiddleware
    participant Handler as Packet Handler

    Client->>TcpListener: Establish socket
    TcpListener->>Protocol: OnAccept(connection)
    Protocol->>Connection: BeginReceive()
    Connection-->>Dispatch: HandlePacket(lease, conn)
    Dispatch->>DispatchChannel: Queue packet (by connection, by prio)
    Note over Dispatch: Signals semaphore\nParallel workers running
    DispatchChannel->>Dispatch: Pull packet (priority order)
    Dispatch->>PacketRegistry: Deserialize to IPacket
    Dispatch->>Middleware: Run inbound pipeline
    Middleware->>Handler: Invoke correct handler
    Handler-->>Middleware: Return result (for outbound)
    Middleware->>Dispatch: Run outbound pipeline (if result)
    Dispatch->>Connection: Send response
```

- **DispatchChannel**: Holds packet queues per connection, tracks priority, performs thread-safe push/pull.
- **DispatchRouter**: Splits workload into “shards” based on connection id for scaling out (think per-core routing).
- **PacketDispatchChannel**: Orchestrates enqueueing, worker loop, packet deserialization, runs middleware, invokes handler, sends response.
- **MiddlewarePipeline**: Pluggable, ordered, chain-of-responsibility pipeline for cross-cutting logic.

---

### 3.2 Packet Processing Pipeline: Middleware & Handler

- **Inbound flow**:  
  `UnwrapPacketMiddleware` → `PermissionMiddleware` → `RateLimitMiddleware` → `ConcurrencyMiddleware` → `TimeoutMiddleware` → [Your Handler]
- **Outbound flow**:  
  Handler Result → `WrapPacketMiddleware` (Encrypt/Compress, etc.)

```mermaid
graph LR
A(Incoming Packet) --> B(UnwrapPacketMiddleware)
B --> C(PermissionMiddleware)
C --> D(RateLimitMiddleware)
D --> E(ConcurrencyMiddleware)
E --> F(TimeoutMiddleware)
F --> G(Handler)
G --> H(WrapPacketMiddleware)
H --> O(Outgoing Response)
```

You control which middleware runs (add your own via configuration).

---

### 3.3 Packet Dispatch Channel/Router (Advanced)

- **Multi-core dispatch**: `PacketDispatchChannel` uses multiple worker loops (up to a configurable maximum, based on CPU count).
- **Per-connection queueing**: Each connection gets its own queue, supporting priority dispatch (`URGENT`, etc.).
- **Ready queues per priority**: Quickly find the highest-priority connection ready for dispatch (no O(n) scans).

---

## 4. Custom Handling & Extensibility

### 4.1. Handler Composition

- Register handler classes using `[PacketController]` + `[PacketOpcode]` attributes.
- Return types supported: `void`, `Task`, `string`, custom packet, etc.
- Handlers are compiled to avoid runtime reflection.

### 4.2. DI & Configuration

- Use `WithHandler`, `WithMiddleware`, `WithErrorHandling`, `WithLogging` extension methods on your dispatch options.

```csharp
var options = new PacketDispatchOptions<IPacket>()
    .WithLogging(logger)
    .WithHandler<MyController>();

var dispatcher = new PacketDispatchChannel(opts => {
    opts.WithMiddleware(new UnwrapPacketMiddleware());
    opts.WithMiddleware(new RateLimitMiddleware());
    opts.WithHandler<CustomHandler>();
    // etc...
});
```

### 4.3. Custom Middleware

Just implement `IPacketMiddleware<TPacket>` and decorate with `[MiddlewareOrder]`, `[MiddlewareStage]` as needed:

```csharp
[MiddlewareOrder(20)]
[MiddlewareStage(MiddlewareStage.Inbound)]
public class CustomLoggingMiddleware : IPacketMiddleware<IPacket> { ... }
```

---

## 5. Error Handling

- At every layer (middleware, handler, outbound): errors are caught, mapped to protocol error codes/advice, and reported to client in a consistent way.
- You can register your own error handlers via `WithErrorHandling`.

---

## 6. Infrastructure Services

### 6.1 FramedSocketChannel

- Abstracts low-level socket receive/send, uses buffer pools to avoid GC.
- Implements efficient read/write, accommodates protocol frame boundaries.

### 6.2 TimingWheel & TimeSynchronizer

- Efficient background timer for auto-disconnecting dead/idle connections (Hashed Wheel Timer pattern).
- Periodic time sync events (for “heartbeat”/liveness logic).

### 6.3 Pooling, Configuration, Logging

- All heavy allocations (packets, connections, buffers, context) can be pooled.
- Centralized config and DI via `InstanceManager`, `ObjectPoolManager`, `ConfigurationManager`.

---

## 7. FAQ / Common Customizations

- **How do I add authentication rate-limiting?**  
  → Write a `RateLimitMiddleware`, insert into pipeline before handler.

- **How do I use my own handler class for certain packets?**  
  → Annotate with `[PacketController]`, register via `.WithHandler<MyHandler>()`.

- **How do I integrate with an external database/cache?**  
  → Your handler/controller class can use DI/resolvers to get the required services.

- **How do I log packet failures?**  
  → Attach a logger via `.WithLogging()`, handle via `.WithErrorHandling()`.

- **How do I process packet priorities?**  
  → Critical packets should set their `PacketPriority` enum value on serialization. The dispatch channel proritizes accordingly.

---

## 8. DDD Breakdown

- **Domain**:  
  Encapsulated in `Packet`, `PacketContext`, `Handler`, Connection as business entities.  
- **Application**:  
  `Protocol`, listener entrypoints, handling orchestration.  
- **Infrastructure**:  
  Sockets, transport, timing, pooling, config, logging—hidden from domain logic.  
- **Shared**:  
  Interfaces, enums, DTO-style packet contracts.

---

## 9. Diagrams: Dispatch & Pipeline

### 9.1. Internal Dispatch Routing

```mermaid
graph LR
    A[PacketDispatchChannel]
    B[DispatchChannel (per shard)]
    C[MiddlewarePipeline]
    D[HandlerMap/Registry]
    E[Connection]
    F[Outbound Middleware]
    A --> B
    B --> C
    C --> D
    D --> E
    D --> F
```

### 9.2. Connection/Packet Lifecycle

```mermaid
graph TD
    SubA[Accept Socket] --> SubB[Create Connection]
    SubB --> SubC[BeginReceive]
    SubC --> SubD[BufferPool/Lease]
    SubD --> SubE[DispatchChannel.Enqueue]
    SubE --> SubF[WorkerLoop Pull/Dispatch]
    SubF --> SubG[Deserialize IPacket]
    SubG --> SubH[Inbound Middleware]
    SubH --> SubI[Packet Handler]
    SubI --> SubJ[Outbound Middleware/Transform]
    SubJ --> SubK[Send to Socket]
```

---

## 10. References & Further Reading

- [Hashed Wheel Timer](https://netty.io/wiki/new-and-noteworthy-in-4.0.html#hashedwheeltimer)
- [Domain-Driven Design reference](https://martinfowler.com/tags/domain%20driven%20design.html)
- [Middleware Pipeline Pattern](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/)
- [Clean Architecture](https://www.freecodecamp.org/news/a-quick-introduction-to-clean-architecture-990c014448d2/)
- [.NET Socket Programming Best Practices](https://learn.microsoft.com/en-us/dotnet/standard/io/asynchronous-file-i-o)

---

> *Nalix.Network: Extensible, high-performance, robust network foundation for modern .NET backends.*
