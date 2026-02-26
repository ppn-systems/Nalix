# Diagnostics Contracts

`Nalix.Common.Diagnostics` defines the core logging contracts used across the stack.

## Source mapping

- `src/Nalix.Common/Logging/INLogixDistributor.cs`
- `src/Nalix.Common/Logging/INLogixErrorHandler.cs`
- `src/Nalix.Common/Logging/INLogixTarget.cs`

## Main types

- `INLogixDistributor`
- `INLogixErrorHandler`
- `INLogixTarget`

## ILogger

`ILogger` is still the main Microsoft logging abstraction used by framework, network, SDK, and logging components.

It exposes methods such as:

- `Trace(...)`
- `Debug(...)`
- `Info(...)`
- `Warn(...)`
- `Error(...)`
- `Critical(...)`

## INLogixTarget

`INLogixTarget` is the sink contract. A target receives a log entry and decides how it is stored or displayed.

Typical examples:

- console output
- file output
- remote logging target

## INLogixDistributor

`INLogixDistributor` is the fan-out abstraction that pushes one log entry to many targets.

It supports:

- `RegisterTarget(...)`
- `UnregisterTarget(...)`
- `Publish(...)`
- `PublishAsync(...)`

## Example

```csharp
ILogger logger = InstanceManager.Instance.Get<ILogger>();

logger.Info("listener started on port {Port}", 57206);
logger.Warn("throttle activated for {Endpoint}", endpoint);
logger.Error(ex, "dispatch failed for {ConnectionId}", connectionId);
```

## Related APIs

- [Logging](../logging/index.md)
- [Logging Options](../logging/options.md)
- [Logging Extensions](../logging/extensions.md)
- [Logging Targets](../logging/targets.md)
