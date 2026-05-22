# Nalix.Logging

## Triggers
- Adding log calls to any Nalix component
- Configuring log sinks or minimum log level
- Debugging missing or dropped log output at shutdown
- Integrating with `ILogger` (Microsoft.Extensions.Logging)

---

## Rules

### Logging API — Extension Methods on `string` and `Exception`
The primary logging surface is `NLogixFx` extension methods, imported from `Nalix.Logging.Extensions`:

```csharp
// Log a string
"Server started on port 8080".Info(nameof(MyClass));
"Connection limit near capacity".Warn(typeof(NetworkManager));
$"Unknown opcode {opcode}".Error(nameof(PacketDispatcher));

// Log an exception
exception.Error(nameof(MyClass), "Unhandled error in handler");
exception.Warn(nameof(SessionHandler), "Retryable failure");
```

Available levels: `Trace`, `Debug`, `Info`, `Warn`, `Error`, `Fatal`

Each method signature:
```csharp
// String overloads
void Info(this string message, string? source = null, object? extendedData = null,
          [CallerMemberName] string callerMemberName = "",
          [CallerFilePath] string callerFilePath = "",
          [CallerLineNumber] int callerLineNumber = 0)

void Info(this string message, Type source, ...)  // Type overload

// Exception overloads
void Error(this Exception ex, string source, string message, ...)
void Error(this Exception ex, Type source, string message, ...)
```

### `NLogixFx` Static Class
Global configuration and publisher:
- `NLogixFx.MinimumLevel` — get/set the global minimum log level
- `NLogixFx.Publisher` — the `NLogixDistributor` instance; call `RegisterTarget()` to add sinks

### `NLogix` Instance Class
`NLogix` implements `ILogger` — use it when a component requires `ILogger` injection:
- `NLogix.Host.Instance` — lazy singleton `NLogix` instance
- `new NLogix(options => { ... })` — create a configured instance

### Log Level Check Before Expensive Formatting
```csharp
if (NLogixFx.MinimumLevel <= LogLevel.Debug)
    $"Packet {packet.Opcode} bytes={size}".Debug(nameof(MyHandler));
```
Without the guard, string interpolation allocates on every call even when Debug is disabled.

### Security — What Must Never Be Logged
- Connection secrets, session keys, any `Bytes32` containing key material
- Cipher suite negotiation parameters
- Passwords, auth tokens, or correlation IDs that could identify a user

---

## Checklists

### Configure logging at startup
```csharp
NLogixFx.MinimumLevel = LogLevel.Information;
NLogixFx.Publisher.RegisterTarget(new BatchConsoleLogTarget());
NLogixFx.Publisher.RegisterTarget(new BatchFileLogTarget("logs/app.log"));
```

### Add logging to a component
```csharp
// Preferred: extension method pattern
"Component initialized".Info(nameof(MyComponent));

// When ILogger injection is needed:
ILogger logger = NLogix.Host.Instance;
// or inject via constructor
```

### Graceful shutdown — flush logs
```csharp
await host.DeactivateAsync();
NLogix.Host.Instance.Dispose(); // flushes distributor before exit
```

---

## Gotchas

- **`NLogix.Info(...)` does not exist**: `NLogix` is an `ILogger` instance class, not a static facade. The static logging API lives on `NLogixFx` as extension methods on `string`/`Exception`. Calling `NLogix.Info(...)` is a compile error.

- **Log entries dropped on abrupt exit**: `NLogixDistributor` is channel-based. If the process exits without calling `Dispose()` on the distributor, queued entries are lost. Always dispose `NLogix.Host.Instance` during graceful shutdown.

- **`BatchFileLogTarget` buffers writes**: File output is batched and not flushed per entry. Recent log entries may not appear in the file immediately. Use `BatchConsoleLogTarget` during debugging for real-time output.

- **String interpolation allocates even when level is filtered**: `$"packet {opcode}"` allocates the string before the level check runs. Gate expensive log messages with an explicit `NLogixFx.MinimumLevel` check.

- **`extendedData` is serialized**: The `object? extendedData` parameter is serialized into the log entry. Passing large objects (e.g., full packet payloads) inflates log storage. Pass only diagnostic-relevant primitives.
