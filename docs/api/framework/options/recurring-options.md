# RecurringOptions

`RecurringOptions` configures recurring job behavior in `TaskManager`.

## Source mapping

- `src/Nalix.Framework/Options/RecurringOptions.cs`

## What it controls

- diagnostic tag
- non-reentrant execution
- startup jitter
- per-run timeout
- failure threshold before backoff
- maximum backoff duration

## Basic usage

```csharp
RecurringOptions options = new()
{
    NonReentrant = true,
    Jitter = TimeSpan.FromMilliseconds(250),
    BackoffCap = TimeSpan.FromSeconds(15)
};
```

## Related APIs

- [Task Manager](../runtime/task-manager.md)
- [WorkerOptions](./worker-options.md)
