# Nalix.Environment

> Bootstrap infrastructure, low-level I/O primitives, and memory management.

**Nalix.Environment** provides the foundational infrastructure for the Nalix stack. It handles environment discovery, secure filesystem operations, AOT-safe configuration loading, and provides the core `DataReader`/`DataWriter` primitives used across the entire framework.

## Key Features

| Component | Purpose | Key Concept / Type |
| :--- | :--- | :--- |
| 🧩 **Memory Primitives** | Zero-copy high-performance reading, writing, and buffer leasing. | `DataReader`, `DataWriter`, `BufferLease` |
| ⚙️ **Configuration** | Fully AOT-safe INI configuration loading and settings binding. | `ConfigurationManager`, `ConfigurationLoader` |
| 🧱 **Fragmentation** | Dynamic reassembly and timeout tracking for large packet chunks. | `FragmentAssembler`, `FragmentHeader` |
| 📂 **Secure IO** | Cross-platform secure filesystem directories and Unix permissions. | `Directories` |
| ⏱️ **Time & Hashing** | Monotonic Unix clock helpers and blazing-fast non-cryptographic hashes. | `Clock`, `XxHash32` |

## Key Namespaces

| Namespace | Purpose | Key Types |
| :--- | :--- | :--- |
| `Nalix.Environment` | Root namespace containing project-wide bootstrapper events | `DiagnosticsEvents` |
| `Nalix.Environment.Memory` | High-performance zero-allocation stream reading/writing and buffer leasing | `DataReader`, `DataWriter`, `BufferLease` |
| `Nalix.Environment.Configuration` & `.Binding` | AOT-safe INI configuration loader and POCO settings binding engines | `ConfigurationManager`, `ConfigurationLoader`, `IniConfig` |
| `Nalix.Environment.Fragments` | Multi-chunk packet fragmentation reassembly and stream lifecycle engines | `FragmentAssembler`, `FragmentHeader`, `FragmentAssemblyResult`, `FragmentStreamId` |
| `Nalix.Environment.IO` | Cross-platform directories setup and Unix filesystem permissions sanitizer | `Directories` |
| `Nalix.Environment.Time` | Monotonic Unix clocks and timing scopes for benchmarking | `Clock`, `TimingScope` |
| `Nalix.Environment.Hashing` | Blazing-fast non-cryptographic hashing algorithms | `XxHash32` |
| `Nalix.Environment.Random` | Thread-safe pseudo-random and cryptographically secure random generators | `Csprng`, `OsCsprng`, `OsRandom` |
| `Nalix.Environment.Sequencing` | Lightweight monotonic sequence validation counters | `SequenceCounter` |
| `Nalix.Environment.Options` | Strongly-typed configuration options POCO models | `MemoryOptions`, `FragmentOptions`, `SequenceOptions` |
| `Nalix.Environment.Diagnostics` | Diagnostics listeners and telemetry providers | `DiagnosticListenerFactory` |

## Installation

```bash
dotnet add package Nalix.Environment
```

## Quick Example: Using DataWriter

```csharp
using System;
using Nalix.Environment.Memory;

// Rent a DataWriter with an initial capacity of 256 bytes from the pool
using var writer = new DataWriter(256);

// Access the free buffer span directly
Span<byte> freeSpace = writer.FreeBuffer;

// Write values directly into the span
freeSpace[0] = 0xAA;
freeSpace[1] = 0xBB;

// Advance the write cursor to commit the 2 written bytes
writer.Advance(2);

// Copy the written data to a tightly-sized array
byte[] encoded = writer.ToArray();
```

## Documentation

For information on memory management and configuration, see the [Environment Documentation](https://ppn-system.me/api/Environment/index).
