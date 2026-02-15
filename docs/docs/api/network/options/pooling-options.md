# PoolingOptions

`PoolingOptions` configures the object pools used by the network runtime.

## Source mapping

- `src/Nalix.Network/Configurations/PoolingOptions.cs`

## What it controls

- accept context capacity and preallocation
- socket async event args capacity and preallocation
- receive context capacity and preallocation
- timeout-task pool sizing
- packet-context pool sizing

## Why it matters

These settings affect:

- startup warmup
- allocation pressure under load
- pool hit rate during bursts

## Basic usage

```csharp
PoolingOptions options = ConfigurationManager.Instance.Get<PoolingOptions>();
options.Validate();
```

## Tuning rule

- `Capacity` should roughly match expected peak concurrent usage
- `Preallocate` should roughly match steady-state warm usage

## Related APIs

- [Timing Wheel](../runtime/timing-wheel.md)
- [Packet Context](../../routing/packet-context.md)
