# Nalix.Environment

> Bootstrap infrastructure, low-level I/O primitives, and memory management.

**Nalix.Environment** provides the foundational infrastructure for the Nalix stack. It handles environment discovery, secure filesystem operations, AOT-safe configuration loading, and provides the core `DataReader`/`DataWriter` primitives used across the entire framework.

## Key Features

| Component | Purpose |
| :--- | :--- |
| 🧩 **Memory Primitives** | `DataReader`, `DataWriter`, and `BufferLease` for zero-copy I/O. |
| ⚙️ **Configuration** | INI-based config system with AOT-safe binding. |
| 🧱 **Fragmentation** | `FragmentAssembler` for handling large packet reassembly. |
| 📂 **Secure IO** | Cross-platform directory and permission management. |
| ⏱️ **Time & Hashing** | Unix clock utilities and non-crypto hashing (XxHash32). |

## Installation

```bash
dotnet add package Nalix.Environment
```

## Quick Example: Using DataWriter

```csharp
using Nalix.Environment.Memory;

// Rents a buffer and writes data efficiently
using var writer = new DataWriter();
writer.WriteInt32(12345);
writer.WriteString("Hello Nalix");

// Get the written span
ReadOnlySpan<byte> encoded = writer.AsSpan();
```

## Documentation

For information on memory management and configuration, see the [Environment Documentation](https://ppn-system.me/api/Environment/index).
