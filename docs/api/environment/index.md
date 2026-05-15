# Nalix.Environment API Reference

`Nalix.Environment` provides the foundational memory, configuration, and cross-platform environmental services.

## Memory & IO

Foundational primitives for high-performance, zero-allocation data handling.

- [**Buffer Management**](./memory/buffer-management.md): High-level byte buffer management.
- [**BufferLease**](./memory/buffer-lease.md): Rented memory segments with reference counting.
- [**DataReader**](./memory/data-reader.md): Fast binary reading from spans and memory.
- [**DataWriter**](./memory/data-writer.md): Fast, growable binary writing.

## Configuration

- [**Configuration**](./configuration.md): INI-based typed options management.

## Platform Helpers

- [**Directories**](./directories.md): Standardized application paths.
- [**Clock**](./clock.md): Monotonic time source.
- [**Random**](./random.md): Thread-safe random number generation.
- [**Timing Scope**](./timing-scope.md): Lightweight latency measurement.

## Related Packages

- [Nalix.Abstractions](../abstractions/index.md)
- [Nalix.Codec](../codec/index.md)
- [Nalix.Framework](../framework/index.md)
- [Nalix.Network](../network/index.md)
- [Nalix.Runtime](../runtime/index.md)
