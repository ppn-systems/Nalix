# 📝 Nalix AI Skill — Logging Infrastructure

This skill covers the `Nalix.Logging` project, a high-performance, asynchronous logging framework designed specifically for networking applications.

---

## 🏗️ Architecture: The NLogix System

Nalix uses "NLogix" for its internal logging.

- **`NLogixDistributor`**: The core engine that receives log events and distributes them to sinks asynchronously.
- **`ILogSink`**: The contract for log destinations (Console, File, Network).
- **`Batching`**: Logs are processed in batches to minimize I/O overhead.

---

## ⚡ High-Performance Logging

To maintain Nalix's performance goals, logging follows strict rules:

- **Async-Only:** All log writes happen on a dedicated background thread to avoid blocking the hot path.
- **Zero-Allocation Sinks:** Custom sinks should avoid allocating strings or intermediate objects.
- **Trace Guards:** Always check `IsTraceEnabled` before performing complex string formatting for trace-level logs.

```csharp
if (logger.IsTraceEnabled)
{
    logger.Trace($"Complex data: {Serialize(obj)}");
}
```

---

## 🛠️ Configuration & Sinks

### Standard Sinks:
- **`ConsoleSink`**: Optimized for high-throughput console output.
- **`FileSink`**: Supports rolling logs and buffered writing.
- **`NullSink`**: Discards all logs (useful for benchmarks).

### Host Integration:
```csharp
builder.UseLogging(opt => {
    opt.MinimumLevel = LogLevel.Info;
    opt.AddConsoleSink();
    opt.AddFileSink("logs/server.log");
});
```

---

## 📜 Log Event Structure

- **Timestamp:** Precise timing of the event.
- **Level:** Severity (Trace, Debug, Info, Warn, Error, Fatal).
- **Tag/Category:** Usually the class name or component.
- **Message:** The descriptive text.
- **Exception:** Optional stack trace for errors.

---

## 🛡️ Common Pitfalls

- **Sync Logging:** Never use `Console.WriteLine` directly in a handler; it is synchronous and slow.
- **Log Flooding:** Avoid logging unique data (like session IDs) at the `Info` level during high-traffic periods; use `Trace` or `Debug`.
- **Large Payloads:** Do not log full packet hex-dumps at levels higher than `Trace`.
