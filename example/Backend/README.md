# Nalix Backend — Example Server

A high-performance, multi-protocol game server built with the [Nalix](https://github.com/nalix) framework. This example demonstrates TCP, UDP, and WebSocket listeners with DDoS protection, connection management, and observability — all running as a **Native AOT** single-file binary with zero runtime dependencies.

---

## Table of Contents

- [Features](#features)
- [Prerequisites](#prerequisites)
- [Quick Start](#quick-start)
- [Build Scripts](#build-scripts)
  - [PowerShell (Windows + Linux)](#powershell-windows--linux)
  - [Bash (Linux / macOS)](#bash-linux--macos)
- [Native AOT Explained](#native-aot-explained)
- [Deployment](#deployment)
  - [Windows](#windows)
  - [Linux x64](#linux-x64)
  - [Raspberry Pi (ARM64)](#raspberry-pi-arm64)
- [Configuration Reference](#configuration-reference)
- [Project Structure](#project-structure)
- [Troubleshooting](#troubleshooting)

---

## Features

| Layer | What's Included |
|-------|-----------------|
| **Transports** | TCP, UDP, WebSocket — all on configurable ports |
| **DDoS Protection** | Per-IP rate limiting (token bucket), connection quotas, progressive banning, IP blacklist/ban persistence |
| **Connection Management** | Sharded `ConnectionHub`, timing-wheel idle cleanup, proxy protocol (v1/v2), forwarded headers |
| **Memory** | Pooled buffers with adaptive growth/shrink, object pooling, trimming on timer |
| **Task Scheduling** | Dynamic worker pool with CPU-aware scaling, busy-wait backoff, dispatch middleware pipeline |
| **Observability** | Built-in metrics, latency tracking, diagnostic channel |
| **Build** | Native AOT single-file output — no .NET runtime on target |

---

## Prerequisites

| Tool | Version | Notes |
|------|---------|-------|
| [.NET SDK](https://dotnet.microsoft.com/download) | **10.0+** | Required for Native AOT compilation |
| [PowerShell 7+](https://github.com/PowerShell/PowerShell) | 7.0+ | For `.ps1` scripts (also ships on Linux/macOS as `pwsh`) |
| Bash | 4.0+ | For `.sh` scripts (Linux/macOS only) |

> **Tip (Windows):** PowerShell 5.1 (built-in) works for basic builds. Install `pwsh` (7+) for the best cross-platform experience.

---

## Quick Start

### Standard build (with .NET runtime)

```bash
# Build and run normally
dotnet run -c Release
```

### Native AOT build (no runtime needed)

```powershell
# Windows
.\build-aot.ps1

# Linux / macOS
./build-aot.sh
```

The binary lands in `publish/<RID>/`:

```
publish/
├── win-x64/Backend.exe      # Windows x64
├── linux-x64/Backend         # Linux x64
└── linux-arm64/Backend       # Linux ARM64 (Pi 5, Graviton, etc.)
```

---

## Build Scripts

### PowerShell (Windows + Linux)

Works natively on Windows. On Linux/macOS, install `pwsh` first:

```bash
# Install pwsh on Ubuntu/Debian
sudo apt install powershell

# Install pwsh on macOS
brew install powershell
```

**Usage:**

```powershell
# Auto-detect current OS and build
.\build-aot.ps1

# Cross-compile for a specific platform
.\build-aot.ps1 -Runtime linux-arm64
.\build-aot.ps1 -Runtime linux-x64
.\build-aot.ps1 -Runtime win-x64

# Clean build (removes obj/bin/publish first)
.\build-aot.ps1 -Runtime linux-x64 -Clean

# Build without IL trimming (useful for diagnosing AOT warnings)
.\build-aot.ps1 -SkipTrim

# Custom output directory
.\build-aot.ps1 -Output "C:\artifacts\server"
```

**Parameters:**

| Parameter | Values | Default | Description |
|-----------|--------|---------|-------------|
| `-Runtime` | `win-x64`, `linux-x64`, `linux-arm64` | Auto-detect | Target platform |
| `-Output` | Any path | `./publish/<RID>` | Output directory |
| `-Clean` | Switch | Off | Wipe obj/bin/publish before build |
| `-SkipTrim` | Switch | Off | Disable IL trimming for AOT diagnostics |

---

### Bash (Linux / macOS)

```bash
# Make executable (first time only)
chmod +x build-aot.sh

# Auto-detect current OS and build
./build-aot.sh

# Cross-compile for a specific platform
./build-aot.sh -r linux-arm64
./build-aot.sh -r linux-x64
./build-aot.sh -r win-x64

# Clean build
./build-aot.sh -r linux-x64 -c

# Build without IL trimming
./build-aot.sh --no-trim

# Custom output directory
./build-aot.sh -o /tmp/backend-artifacts
```

**Options:**

| Option | Values | Default | Description |
|--------|--------|---------|-------------|
| `-r, --runtime` | `win-x64`, `linux-x64`, `linux-arm64` | Auto-detect | Target platform |
| `-o, --output` | Any path | `./publish/<RID>` | Output directory |
| `-c, --clean` | Flag | Off | Wipe obj/bin/publish before build |
| `--no-trim` | Flag | Off | Disable IL trimming |
| `-h, --help` | Flag | — | Show usage info |

---

## Native AOT Explained

When you build with the AOT scripts, the .NET ILC (IL Compiler) ahead-of-time compiles all IL to native machine code. The result is:

| Metric | Value |
|--------|-------|
| Binary size | ~11 MB (single file, compressed) |
| Cold start | < 100ms |
| Runtime dependency | **None** — just copy and run |
| Supported .NET | 10.0+ |

**What gets optimized:**

- `PublishAot=true` — full ahead-of-time compilation
- `PublishTrimmed` + `TrimMode=full` — removes unused IL
- `PublishSingleFile` + compression — one file to deploy
- `DebuggerSupport=false` — strips debug plumbing
- `IlcOptimizationPreference=Speed` — optimizes for throughput over size
- `IlcFoldIdenticalMethodBodies=true` — deduplicates identical methods
- `StripSymbols=true` — removes debug symbols from output

**AOT toggle mechanism:**

The scripts pass `/p:AotBuild=true` to `dotnet publish`. This is a **local** MSBuild property consumed only by `Backend.csproj` — it does not propagate to analyzer/library dependencies (which would cause `NETSDK1124` on `netstandard2.0` projects).

```
build script          Backend.csproj              Analyzer (netstandard2.0)
─────────────         ──────────────              ────────────────────────
/p:AotBuild=true  →   PublishAot=true             (not affected ✓)
                      PublishTrimmed=true
                      IlcOptimizationPreference=Speed
                      ...
```

---

## Deployment

### Windows

```powershell
# Build
.\build-aot.ps1

# Run
.\publish\win-x64\Backend.exe
```

No .NET runtime installation needed.

### Linux x64

```bash
# Build (on Linux or cross-compile from Windows)
./build-aot.sh -r linux-x64

# Deploy
scp publish/linux-x64/Backend user@server:~/

# Run on server
chmod +x ~/Backend
./Backend
```

### Raspberry Pi (ARM64)

```powershell
# Cross-compile from Windows
.\build-aot.ps1 -Runtime linux-arm64

# Upload to Pi
scp publish\linux-arm64\Backend pi@192.168.1.100:~/

# Run on Pi
ssh pi@192.168.1.100
chmod +x ~/Backend
./Backend
```

---

## Configuration Reference

All options are configured in `Startup.cs`. Key sections:

### Network & Listeners

| Option | Default | Description |
|--------|---------|-------------|
| `NetworkSocketOptions.Port` | `57206` | TCP + UDP listen port |
| `NetworkSocketOptions.Backlog` | `16384` | OS-level TCP accept backlog |
| `NetworkSocketOptions.MaxParallel` | `8` | Parallel accept workers |
| `NetworkWebSocketOptions.Port` | `57207` | WebSocket listen port |
| `NetworkWebSocketOptions.Path` | `/ws/` | WebSocket upgrade path |

### DDoS Protection

| Option | Default | Description |
|--------|---------|-------------|
| `ConnectionGuardOptions.MaxConnections` | `2000` | Global concurrent connection cap |
| `ConnectionGuardOptions.MaxPacketPerSecond` | `30` | Per-connection packet rate limit |
| `ConnectionQuotaOptions.MaxConnectionsPerIpAddress` | `32` | Per-IP connection limit |
| `ConnectionQuotaOptions.MaxConnectionsPerWindow` | `50` | New connections per 5s window |
| `TokenBucketOptions.CapacityTokens` | `1000` | Token bucket burst capacity |
| `TokenBucketOptions.RefillTokensPerSecond` | `100` | Token refill rate |

### Memory

| Option | Default | Description |
|--------|---------|-------------|
| `BufferOptions.TotalBuffers` | `20000` | Maximum pooled buffers |
| `BufferOptions.MaxMemoryPercentage` | `0.60` | Max heap % for buffer pool |
| `BufferOptions.BufferAllocations` | `64,0.15; 256,0.15; ...` | Size-class distribution |
| `ObjectPoolOptions.DefaultPreallocate` | `1000` | Pre-allocate per pool |
| `ObjectPoolOptions.DefaultMaxPoolSize` | `20000` | Max objects per pool |

### Task Scheduling

| Option | Default | Description |
|--------|---------|-------------|
| `TaskManagerOptions.MaxWorkers` | `CPU×32 (min 128)` | Thread pool ceiling |
| `DispatchOptions.MaxPerConnectionQueue` | `128` | Per-connection dispatch cap |
| Dispatch loop count | `8` | Parallel dispatch loops |

---

## Project Structure

```
Backend/
├── Backend.csproj          # Project file with AOT toggle
├── Program.cs              # Entry point
├── Startup.cs              # All configuration & listener binding
├── Attributes/
│   └── PacketTagAttribute.cs
├── Middleware/
│   └── PacketTagMiddleware.cs
├── Properties/
│   └── launchSettings.json
├── build-aot.ps1           # ← Native AOT build (PowerShell)
├── build-aot.sh            # ← Native AOT build (Bash)
└── README.md               # This file
```

---

## Troubleshooting

### `NETSDK1124: Trimming assemblies requires .NET Core 3.0 or higher`

You're passing `/p:PublishAot=true` or `/p:PublishTrimmed=true` as a global property. This leaks to `netstandard2.0` analyzer projects. Use the provided build scripts (which pass `/p:AotBuild=true` instead) or set `AotBuild=true` in the csproj directly.

### AOT build warns about `IL2091` (SingletonBase generic constraint)

This is a known trim analysis warning for `SingletonBase<T>` with unconstrained generics. It does **not** affect runtime behavior — the source-generated activators handle instance creation without reflection.

### Binary won't start on target machine

Make sure you built for the correct RID:
- `win-x64` → Windows 10/11 x64
- `linux-x64` → Ubuntu 22.04+, Debian 12+, RHEL 9+, etc.
- `linux-arm64` → Raspberry Pi 4/5, AWS Graviton, Apple Silicon Linux

### `TrimmerDefaultAction` is deprecated

Use `TrimMode` instead. This was fixed in the current csproj — if you see this warning, make sure you have the latest version.

### How do I build without AOT (normal .NET runtime)?

Just use `dotnet run` or `dotnet publish` directly — the `AotBuild` property defaults to `false`:

```bash
dotnet publish -c Release -r linux-x64 --self-contained true
```