# Nalix Packet Visualizer

The **Packet Visualizer** is a developer tool designed to capture and decode Nalix network frames. It provides a human-readable view of the raw byte stream, allowing for easier debugging of handshakes, message routing, and frame fragmentation.

## Features

- **Real-time Capture**: Intercept and display frames as they flow through a local listener or SDK session.
- **Protocol Decoding**: Automatically decodes standard Nalix headers including `FrameType`, `PayloadLength`, and `SequenceID`.
- **Field Inspector**: Click on a frame to see a detailed breakdown of its bitwise components.
- **Color-coded Frames**: Distinguished visual styles for `Control`, `Data`, `Signal`, and `Fragment` frames.

## Getting Started

To launch the visualizer:

1. Navigate to `tools/Nalix.PacketVisualizer`.
2. Run the project:
   ```powershell
   dotnet run
   ```

## Usage Scanning

The visualizer can be attached to any running Nalix application that enables **Diagnostic Hooks**.

### Enabling Hooks in Code
To allow the visualizer to see your traffic, enable the diagnostic output in your `ServerOptions` or `ConnectionOptions`:

```csharp
var options = new ServerOptions();
options.Diagnostics.EnableFrameHooks = true;
```

## Internal Architecture

The visualizer uses a low-overhead sampling mechanism to ensure that inspecting packets does not significantly impact the performance of the system being measured. It is strictly a **passive observer** and does not modify network traffic.
