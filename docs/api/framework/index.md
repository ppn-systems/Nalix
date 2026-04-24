# Nalix.Framework API Reference

`Nalix.Framework` provides the core runtime services for the Nalix ecosystem, including dependency injection, background tasks, and system-wide identifiers.

## System Infrastructure

The framework layer is designed to be transport-agnostic and provides the following core capabilities:

- [**Instance Manager (DI)**](./instance-manager.md): Zero-allocation service registry optimized for hot paths.
- [**Task Manager**](./task-manager.md): Comprehensive background worker and recurring job management.
- [**Snowflake**](./snowflake.md): 64-bit compact, sortable IDs for tasks and packets.
- [**Clock**](../environment/clock.md): Monotonic time source for network timing.

## Configuration & Environment

Configuration and environmental helpers have moved to [Nalix.Environment](../environment/directories.md).

- [**Configuration**](../environment/configuration.md): High-performance INI-based runtime with automatic reloading.

## Data and Codec

Memory management and serialization components have moved to [Nalix.Codec](./memory/buffer-management.md).

- [**Object Pooling**](./memory/object-pooling.md): Reusable object stores to minimize GC pressure.
- [**Object Map**](./memory/object-map.md): High-performance pooled concurrent dictionaries.
- [**Typed Object Pools**](./memory/typed-object-pools.md): Performance-optimized typed facades for pooling.

## Related Packages

- [Nalix.Abstractions](../abstractions/index.md)
- [Nalix.Environment](../environment/directories.md)
- [Nalix.Codec](./memory/buffer-management.md)
- [Nalix.Network](../network/index.md)
- [Nalix.Runtime](../runtime/index.md)


