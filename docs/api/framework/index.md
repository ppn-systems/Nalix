# Nalix.Framework API Reference

`Nalix.Framework` provides the foundation for the entire ecosystem, including configuration, dependency injection, background tasks, and high-performance memory management.

## System Infrastructure

The framework layer is designed to be transport-agnostic and provides the following core capabilities:

- [**Directories**](./environment/directories.md): Unified path resolution for configuration, logs, data, and storage.
- [**Configuration**](./runtime/configuration.md): High-performance INI-based runtime with automatic reloading.
- [**Instance Manager (DI)**](./runtime/instance-manager.md): Zero-allocation service registry optimized for hot paths.
- [**Task Manager**](./runtime/task-manager.md): Comprehensive background worker and recurring job management.

## Data and Memory

- [**Buffer Management**](./memory/buffer-management.md): High-performance byte buffer pools and leases.
- [**Object Pooling**](./memory/object-pooling.md): Reusable object stores to minimize GC pressure.
- [**Snowflake**](./runtime/snowflake.md): 56-bit compact, sortable IDs for tasks and packets.

## Related Packages

- [Nalix.Common](../common/index.md)
- [Nalix.Network](../network/index.md)
- [Nalix.Runtime](../runtime/index.md)
