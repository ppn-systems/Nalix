# Packet Serialization Inspector

!!! info "Learning Signals"
    - :fontawesome-solid-layer-group: **Level**: Intermediate
    - :fontawesome-solid-clock: **Time**: 5 minutes
    - :fontawesome-solid-book: **Prerequisites**: [Packet System](../concepts/fundamentals/packet-system.md)

The **Packet Serialization Inspector** (internally known as `Nalix.PacketVisualizer`) is a developer utility designed to verify and visualize how C# packet properties map to raw binary buffers. It is a critical tool for debugging custom serialization logic and ensuring binary compatibility.

---

## 🔍 Overview

Unlike a network sniffer, this tool focuses on the **serialization boundary**. It allows you to:

-   **Load Contracts**: Import any DLL containing `IPacket` implementations.
-   **Edit Fields**: Use a visual property grid to modify packet data.
-   **Live Hex Preview**: See the resulting binary buffer update in real-time as you change properties.
-   **Stress Testing**: Use the "Randomize" feature to test serialization with high-entropy or edge-case data.

---

## 🚀 Getting Started

To launch the inspector:

1.  Navigate to `tools/Nalix.PacketVisualizer`.
2.  Run the project:
    ```powershell
    dotnet run
    ```
3.  Click **"Load DLL"** and select your assembly (e.g., `MyApp.Contracts.dll`).

---

## ✨ Key Features

### Dynamic DLL Loading
Load any assembly containing Nalix packets at runtime. The tool automatically discovers all implementations of `IPacket` and populates the selection list.

### Real-time Binary Mapping
The "Hex View" provides a bit-perfect representation of the `Serialize()` output. This helps you verify that:
-   Property order is correct.
-   Variable-length fields (Strings, Byte Arrays) are sized correctly.
-   Magic numbers and headers are properly positioned.

### Randomization & Validation
The **Randomize** button populates all writable properties with randomized data while respecting attributes like `[SerializeDynamicSize]`. This is ideal for checking if varying payload sizes impact your frame boundaries.

---

## 🛠️ Internal Mechanics

The tool uses a passive reflection mechanism to instantiate and serialize packets.

| Phase | Action |
|:---|:---|
| **Discovery** | Scans `AppDomain` or loaded assembly for `IPacket`. |
| **Instantiation** | Creates a default instance via parameterless constructor. |
| **Observation** | Hooks into the `PropertyValueChanged` event of the PropertyGrid. |
| **Visualization** | Invokes `.Serialize()` and updates the hex display. |

!!! tip "Debugging Custom Packets"
    If you've implemented a custom `Serialize()` method, use this tool to ensure that your manual byte manipulations match your expectations before deploying to a live server.
