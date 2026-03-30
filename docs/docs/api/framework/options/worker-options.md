# WorkerOptions

`WorkerOptions` configures one-off or long-running worker scheduling in `TaskManager`.

## Source mapping

- `src/Nalix.Framework/Options/WorkerOptions.cs`

## What it controls

- worker tag
- machine ID
- `SnowflakeType`
- execution timeout
- post-completion retention
- per-group concurrency limit
- immediate vs waiting slot acquisition
- cancellation token
- completion and failure callbacks

## Basic usage

```csharp
WorkerOptions options = new()
{
    Tag = "import",
    GroupConcurrencyLimit = 2,
    TryAcquireSlotImmediately = false,
    RetainFor = TimeSpan.FromMinutes(5)
};
```

## Related APIs

- [Task Manager](../runtime/task-manager.md)
- [RecurringOptions](./recurring-options.md)
