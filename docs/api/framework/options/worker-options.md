# WorkerOptions

`WorkerOptions` configures one-off or long-running worker scheduling in `TaskManager`.

## Source mapping

- `src/Nalix.Framework/Options/WorkerOptions.cs`

## What it controls

- worker tag
- machine ID
- `SnowflakeType` (identifier type)
- scheduler priority
- OS-level thread priority
- execution timeout
- post-completion retention
- per-group concurrency limit
- immediate vs waiting slot acquisition
- cancellation token
- completion and failure callbacks

## Key Members

| Property | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `Tag` | `string?` | `null` | Optional tag for identifying the worker. |
| `MachineId` | `ushort` | `1` | Machine identifier for the worker instance. |
| `IdType` | `SnowflakeType` | `System` | Identifier type for the worker instance. |
| `Priority` | `WorkerPriority` | `NORMAL` | Scheduler priority while the worker is queued. |
| `OSPriority` | `ThreadPriority?` | `null` | Optional OS-level thread priority. When set, the worker runs on a dedicated thread. |
| `ExecutionTimeout` | `TimeSpan?` | `null` | Optional execution timeout; the worker is cancelled if exceeded. |
| `RetainFor` | `TimeSpan?` | `2 min` | Duration finished workers are retained for diagnostics. Set to `null` or `Zero` to auto-remove. |
| `GroupConcurrencyLimit` | `int?` | `null` | Per-group concurrency cap. If set, executions in this group are gated. |
| `TryAcquireSlotImmediately` | `bool` | `false` | If `true`, cancel immediately when group slot is unavailable; otherwise wait. |
| `CancellationToken` | `CancellationToken` | `None` | Token linked to the worker's execution. |
| `OnCompleted` | `Action<IWorkerHandle>?` | `null` | Callback invoked when the worker completes successfully. |
| `OnFailed` | `Action<IWorkerHandle, Exception>?` | `null` | Callback invoked when the worker fails. |

## Basic usage

```csharp
WorkerOptions options = new()
{
    Tag = "import",
    MachineId = 1,
    IdType = SnowflakeType.System,
    Priority = WorkerPriority.HIGH,
    GroupConcurrencyLimit = 2,
    TryAcquireSlotImmediately = false,
    RetainFor = TimeSpan.FromMinutes(5)
};
```

## Related APIs

- [Task Manager](../task-manager.md)
- [RecurringOptions](./recurring-options.md)
