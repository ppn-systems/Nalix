# RecurringOptions

`RecurringOptions` configures recurring job behavior in `TaskManager`.

## Source mapping

- `src/Nalix.Framework/Options/RecurringOptions.cs`

## What it controls

- diagnostic tag
- non-reentrant execution
- startup jitter
- per-run execution timeout
- failure threshold before backoff
- maximum backoff duration

## Key Members

| Property | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `Tag` | `string?` | `null` | Optional tag for identifying the recurring task. |
| `NonReentrant` | `bool` | `true` | If `true`, prevents overlapping executions of the same recurring job. |
| `Jitter` | `TimeSpan?` | `250 ms` | Optional jitter to randomize the start time. |
| `ExecutionTimeout` | `TimeSpan?` | `null` | Optional timeout for a single run; the run is cancelled if exceeded. |
| `FailuresBeforeBackoff` | `int` | `1` | Number of consecutive failures before exponential backoff is applied. |
| `BackoffCap` | `TimeSpan` | `15 s` | Maximum backoff duration after consecutive failures. |

## Basic usage

```csharp
RecurringOptions options = new()
{
    NonReentrant = true,
    Jitter = TimeSpan.FromMilliseconds(250),
    ExecutionTimeout = TimeSpan.FromSeconds(30),
    FailuresBeforeBackoff = 3,
    BackoffCap = TimeSpan.FromSeconds(15)
};
```

## Related APIs

- [Task Manager](../task-manager.md)
- [WorkerOptions](./worker-options.md)
