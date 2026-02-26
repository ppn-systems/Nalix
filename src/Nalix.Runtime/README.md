# Nalix.Runtime

`Nalix.Runtime` provides the core packet processing and middleware infrastructure for Nalix. It includes the middleware pipeline orchestration, packet dispatching channels, and execution management needed to handle message-oriented traffic with high efficiency.

## Install

```bash
dotnet add package Nalix.Runtime
```

## What it includes

- High-performance middleware pipeline orchestration
- Reliable packet dispatch channels and routing
- Packet context and low-allocation metadata management
- Execution and synchronization management for packet processing
- Support for specialized network buffer middleware
- Flexible options for tuning dispatch behavior and concurrency

## Typical use

Add this package when you need to build custom packet processing logic, middleware, or specialized packet dispatchers within the Nalix ecosystem.

While typically used internally by `Nalix.Network.Hosting`, it can be used standalone to build complex message-processing workflows independent of specific transport implementations.

## Documentation

- Package docs: [Nalix.Runtime](https://ppn-systems.github.io/Nalix/packages/nalix-runtime/)
- API docs: [Runtime API](https://ppn-systems.github.io/Nalix/api/runtime/dispatching/packet-dispatch-channel/)
