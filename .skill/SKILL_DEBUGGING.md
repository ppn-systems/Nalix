# 🔍 Nalix AI Skill — Debugging & Diagnostics

This skill provides guidance on troubleshooting, logging, and monitoring the Nalix framework in both development and production environments.

---

## 📝 Structured Logging

Nalix uses an asynchronous, high-throughput logging system (`Nalix.Logging`).

- **Sinks:** Logs can be directed to Console, File, or custom sinks.
- **Levels:** Use `Trace` for hot-path data, `Debug` for internal state, and `Warn`/`Error` for production issues.
- **Zero-Allocation Logging:** Use formatted strings with caution. Prefer passing state objects if the sink supports structured logging.

---

## 📊 Diagnostic Dashboard

Nalix provides built-in metrics for key components. You can access these via:

- **`ObjectPoolManager.GetStatistics()`**: Monitor pool hits/misses and leakage.
- **`BufferPoolManager.GetStatistics()`**: Monitor memory usage and fragmentation.
- **`TaskManager.GetStatistics()`**: Monitor thread usage and task throughput.

### Console Output Pattern:
The framework often uses a "Dashboard" style output for console applications:
```text
[Pool] Created: 1,200 | Rented: 450,000 | Available: 800
[Net]  IO Sent: 45GB  | IO Recv: 42GB   | Connections: 5,000
```

---

## 🛡️ Troubleshooting Common Issues

### 1. Connection Drops
- **Cause:** Handshake timeout or encryption mismatch.
- **Check:** `TransportOptions.ConnectTimeoutMillis` and `EncryptionEnabled`.
- **Log:** Look for `ControlType.ERROR` packets or `SocketException`.

### 2. Memory Growth
- **Cause:** Leaking `IBufferLease` or failing to return objects to `ObjectPoolManager`.
- **Check:** Ensure every `Rent` has a corresponding `Return` or `Dispose` (NALIX039).
- **Tool:** Use the analyzer to find potential leaks.

### 3. High Latency
- **Cause:** Garbage Collection or thread pool starvation.
- **Check:** Ensure `NoDelay = true` (TCP) and that you are not allocating in the hot path (NALIX037).

---

## 🛠️ Diagnostics Tooling

- **`Nalix.PacketVisualizer`**: A tool to inspect and replay raw binary packets.
- **Roslyn Analyzers**: Real-time feedback in the IDE to prevent common usage errors.
- **Benchmarks**: Always run `Nalix.Benchmarks` to verify that a change hasn't introduced a performance regression.

---

## 🧪 AI Debugging Strategy

1. **Check Analyzers:** First, ensure the code is free of `NALIX-XXX` warnings.
2. **Review Metrics:** Look at pool statistics to see if something isn't being returned.
3. **Trace Dispatch:** Enable `Trace` logging in `Nalix.Runtime` to see exactly where a packet is being dropped in the pipeline.
