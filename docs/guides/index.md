# Guides Overview

These guides walk through real, runnable samples — start with the one closest to what you're building.

## Start here

- [Build a Chat Room](./build-a-chat-room.md) — broadcast messages to every connected client
- [Securing Your Server](./securing-your-server.md) — enable encryption and run TCP, UDP, and WebSocket together

## Application logic

- [Implementing Packet Handlers](./application/packet-handlers.md) — writing business logic and managing opcodes
- [Middleware Usage Guide](./application/middleware-usage.md) — enforcing policy across handlers

## Networking

- [Client Session Guide](./networking/connecting-clients.md) — connecting TCP/UDP sessions with `Nalix.SDK`
- [UDP Server Guide](./networking/udp-server.md) — building low-latency datagram services
- [TCP Patterns Guide](./networking/tcp-patterns.md) — request/response and manual listener wiring
- [UDP Security Guide](./networking/udp-security.md) — secure session handover for UDP
- [Idle Timeout Configuration](./networking/idle-timeout-configuration.md) — tuning connection timeouts

## Extensibility

- [Custom Middleware Guide](./extensibility/custom-middleware.md) — building your own pipeline components
- [Custom Metadata Providers](./extensibility/metadata-providers.md) — using attributes to drive custom behavior
- [Custom Packet Router](./extensibility/custom-packet-router.md) — routing packets outside the default dispatcher
- [Custom Serialization Provider](./extensibility/serialization-providers.md) — registering custom formatters

## Getting started references

- [Project Setup](./getting-started/project-setup.md) — structuring a new Nalix solution
- [Server Blueprint](./getting-started/server-blueprint.md) — a standard startup sequence
- [Server Boilerplate](./getting-started/server-boilerplate.md) — a starting-point server template

## Deployment & operations

- [Production Server Example](./deployment/production-example.md) — a more complete example server
- [Production Checklist](./deployment/production-checklist.md) — a release gate before you ship
- [Troubleshooting Guide](./deployment/troubleshooting.md) — common issues and where to look first
