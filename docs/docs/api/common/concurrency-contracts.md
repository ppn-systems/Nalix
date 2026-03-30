# Concurrency Contracts

This page covers the shared background-work contracts in `Nalix.Common.Concurrency`.

## Source mapping

- `src/Nalix.Common/Concurrency/ITaskManager.cs`
- `src/Nalix.Common/Concurrency/IWorkerHandle.cs`
- `src/Nalix.Common/Concurrency/IWorkerContext.cs`
- `src/Nalix.Common/Concurrency/IRecurringHandle.cs`
- `src/Nalix.Common/Concurrency/IWorkerOptions.cs`
- `src/Nalix.Common/Concurrency/IRecurringOptions.cs`

## Main types

- `ITaskManager`
- `IWorkerHandle`
- `IWorkerContext`
- `IRecurringHandle`
- `IWorkerOptions`
- `IRecurringOptions`

## ITaskManager

`ITaskManager` is the shared contract behind long-running workers and recurring jobs.

## Basic usage

```csharp
IWorkerHandle worker = taskManager.ScheduleWorker(
    "cleanup",
    "maintenance",
    async (ctx, ct) =>
    {
        ctx.Beat();
        ctx.Advance(1, "started");
        await CleanupAsync(ct);
    });

IRecurringHandle recurring = taskManager.ScheduleRecurring(
    "heartbeat",
    TimeSpan.FromSeconds(10),
    async ct => await SendHeartbeatAsync(ct));
```

### Public methods that matter

- `ScheduleRecurring(...)`
- `RunOnceAsync(...)`
- `ScheduleWorker(...)`
- `CancelAllWorkers()`
- `CancelWorker(id)`
- `CancelGroup(group)`
- `CancelRecurring(name)`
- `GetWorkers(...)`
- `TryGetWorker(id, out handle)`
- `GetRecurring()`
- `TryGetRecurring(name, out handle)`

## IWorkerContext

`IWorkerContext` is passed into worker delegates so they can report heartbeat and progress.

## Example

```csharp
ctx.Beat();
ctx.Advance(5, "batch completed");
```

## Handle contracts

`IWorkerHandle` and `IRecurringHandle` expose status snapshots for running jobs.

You typically read:

- `Id`
- `Name`
- `Group`
- `IsRunning`
- `Progress`
- `LastRunUtc`
- `NextRunUtc`

## Related APIs

- [Task Manager](../framework/runtime/task-manager.md)
- [Worker Options](../framework/options/worker-options.md)
- [Recurring Options](../framework/options/recurring-options.md)
