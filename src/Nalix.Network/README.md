# Nalix.Network

`Nalix.Network` is the server-side networking runtime for Nalix. It provides listeners, protocol flow, packet dispatch, connection management, and transport-facing runtime controls for building scalable distributed services.

## Install

```bash
dotnet add package Nalix.Network
```

## What it includes

- TCP and UDP listener runtime
- Protocol processing and packet dispatch
- Connection and connection hub management
- Routing, packet metadata, and handler integration
- Transport diagnostics and runtime configuration

## Typical use

Add this package when you are building the server side of a Nalix-based system and need the core networking runtime.

For packet middleware, throttling, and time-sync helpers, pair it with `Nalix.Network.Pipeline`.

## Documentation

- Package docs: [Nalix.Network](https://ppn-systems.github.io/Nalix/packages/nalix-network/)
- API docs: [Network API](https://ppn-systems.github.io/Nalix/api/network/runtime/tcp-listener/)
