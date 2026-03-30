# Nalix.Logging

Structured logging, built-in targets, and a shared `ILogger` implementation.

### Logging bootstrap
Register the logger once and reuse it across the process.

**Key Components**
- `NLogix`
- `NLogix.Host`
- `ILogger`

### Quick example

```csharp
InstanceManager.Instance.Register<ILogger>(NLogix.Host.Instance);
```

### Options and targets
Options describe log level, formatting, and targets.

**Key Components**
- `NLogixOptions`
- `FileLogOptions`
- `ILoggerTarget`

### Quick example

```csharp
NLogixOptions options = new();
ILoggerTarget target = /* your target */;
options.RegisterTarget(target);
```

!!! warning "Dispose with care"
    Dispose log options only after all targets are registered and stable.

## Key API pages

- [Logging](../api/logging/index.md)
- [Logging Options](../api/logging/options.md)
- [Logging Extensions](../api/logging/extensions.md)
- [Logging Targets](../api/logging/targets.md)
