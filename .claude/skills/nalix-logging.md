# Nalix.Logging

## Triggers
- Adding log calls anywhere in the codebase
- Configuring log sinks or log levels
- Debugging missing or dropped log output
- Shutting down the application

---

## Rules

### Entry Point
- All logging goes through `NLogix` static facade — never use `Console.WriteLine`, `Debug.WriteLine`, or `ILogger` directly in Nalix projects
- `NLogix` is non-blocking: calls enqueue to a `System.Threading.Channels` channel and return immediately — the caller thread is never blocked by I/O

### Log Levels
- `Trace`: raw byte buffers, header dumps, per-packet detail — disabled in production
- `Debug`: developer flow events, state transitions, handler routing
- `Info`: normal lifecycle events (server start, connection accepted, session created)
- `Warn`: degraded operation, pool miss rate spike, soft throttle triggered
- `Error`: recoverable errors, handler exceptions
- `Critical`: unrecoverable errors requiring immediate operator attention

### Security — What Must Never Be Logged
- Connection secrets or session keys
- Cipher suite negotiation parameters
- Any `Bytes32` value that contains key material
- Passwords, tokens, or correlation IDs that could identify a user

### Shutdown
`NLogix` uses a background consumer to drain the log channel to sinks. If the process exits without flushing, queued log entries are dropped.

**Always call `NLogix.Host.StopAsync()` during graceful shutdown** — this drains the channel before the process exits.

---

## Checklists

### Add logging to a new component
1. Use `NLogix.Info(...)`, `NLogix.Warn(...)`, etc. — no constructor injection needed
2. For high-frequency paths: check `NLogix.IsEnabled(LogLevel.Debug)` before constructing the message string
3. Never log: secrets, keys, tokens, passwords

### Configure sinks
```csharp
NLogix.Host.Configure(options => {
    options.MinimumLevel = LogLevel.Info;
    options.AddSink(new BatchConsoleLogTarget());
    options.AddSink(new BatchFileLogTarget("logs/app.log"));
});
await NLogix.Host.StartAsync();
```

### Graceful shutdown with log flush
```csharp
await host.DeactivateAsync();
await NLogix.Host.StopAsync(); // drain channel before exit
```

---

## Gotchas

- **Log entries dropped on abrupt exit**: The channel consumer is a background task. If the process is killed or throws an unhandled exception that bypasses `StopAsync()`, queued log entries are lost. This is by design for performance but means logs near crash time may be missing.

- **Level check before string formatting on hot paths**: `NLogix.Debug($"packet {packet.Opcode}")` formats the interpolated string even if Debug level is disabled. On paths called >10,000/sec, this allocation adds up. Gate with `if (NLogix.IsEnabled(LogLevel.Debug))`.

- **`BatchFileLogTarget` buffers writes**: File output is batched and flushed on an interval, not per-log-entry. Recent entries may not appear in the file immediately after they're written. Do not tail the log file as a real-time stream during debugging — use console sink instead.

- **Logging inside `Reset()` or pool return is dangerous**: Pool return paths call `Reset()` and then reclaim the object. If `Reset()` logs something that triggers pool activity, it can cause re-entrant pool access. Avoid logging inside pool lifecycle methods.
