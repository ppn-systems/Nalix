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
