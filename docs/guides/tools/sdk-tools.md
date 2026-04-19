# SDK Developer Tools

!!! info "Learning Signals"
    - :fontawesome-solid-layer-group: **Level**: Advanced
    - :fontawesome-solid-clock: **Time**: 10 minutes
    - :fontawesome-solid-book: **Prerequisites**: [Introduction](../introduction.md), [Architecture](../concepts/fundamentals/architecture.md)

The **Nalix SDK Tools** is a comprehensive WPF-based desktop suite designed for professional developers to monitor, debug, and interact with live Nalix infrastructure. It provides a "command center" view of your networking environment.

---

## 🖥️ Overview

The SDK Tools application consolidates several specialized utilities into a single, high-performance interface. It is built using the MVVM pattern for responsiveness and reliability during high-frequency data streaming.

### ✨ Key Features

- 🏗️ **Packet Builder**: Design and dispatch custom network packets with a real-time field editor.
- 🔍 **Hex Viewer Overlay**: Deep-dive into raw binary payloads with professional-grade hex visualization.
- 📡 **Registry Browser**: Explore all registered packet types, handlers, and protocols in your solution.
- 📜 **Log Monitor**: High-performance log streaming with advanced filtering and search.
- 🕒 **Packet History**: Record, analyze, and replay network communication sequences.

---

## 🚀 Getting Started

The SDK Tools application requires a Windows environment as it is built on **WPF**.

### 1. Run from Source
Navigate to the tool directory and use the .NET CLI:

```bash
cd tools/Nalix.SDK.Tools
dotnet run
```

---

## 🛠️ Tool Components

### Packet Builder

The Packet Builder allows you to manually construct any packet defined in your contracts. This is useful for:

- Testing specific handler logic without writing a full client.
- Sending malformed or edge-case payloads to verify server robustness.

### Registry Browser

The Registry Browser reflects on your loaded assemblies to show a complete map of your packet ecosystem. You can see:

- Every `[Packet]` and its unique **Magic Number**.
- Which `[PacketController]` handles which opcode.
- The internal structure and fields of all protocol messages.

### Log Monitor & History

Connect the SDK Tools to a running server or client to stream diagnostic logs. The **Packet History** tab keeps a rolling buffer of recently processed frames, allowing you to "time-travel" through a communication sequence to find the exact point of failure.

---

## 📋 Prerequisites

-   **.NET 10 SDK**
-   **Windows OS** (WPF requirement)
-   **Contracts Assembly**: You will need to point the tool to your compiled DLLs to enable the Builder and Browser features.

!!! danger "Production Warning"
    While the SDK Tools are powerful for development and staging, ensure that diagnostic ports (used by the Log Monitor) are not exposed on public-facing production servers unless secured via VPN or SSH tunnel.
