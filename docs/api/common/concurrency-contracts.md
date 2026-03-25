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

## Public members at a glance

| Type | Public members |
| --- | --- |
| `ITaskManager` | `Title`, `ScheduleRecurring(...)`, `RunOnceAsync(...)`, `ScheduleWorker(...)`, `CancelAllWorkers()`, `CancelWorker(...)`, `CancelGroup(...)`, `CancelRecurring(...)`, `GetWorkers(...)`, `TryGetWorker(...)`, `GetRecurring()`, `TryGetRecurring(...)` |
| `IWorkerContext` | `Beat()`, `Advance(...)`, and worker progress/heartbeat reporting members used by the current runtime |
| `IWorkerHandle` | worker identity, group, running state, progress, last run, next run, and cancellation/reporting helpers |
| `IRecurringHandle` | recurring job identity, next run, running state, and cancellation/reporting helpers |
| `IWorkerOptions` | `Tag`, `MachineId`, `IdType`, `Priority`, `OnCompleted`, `OnFailed`, `ExecutionTimeout`, `RetainFor`, `GroupConcurrencyLimit`, `TryAcquireSlotImmediately`, `CancellationToken` |
| `IRecurringOptions` | `Tag`, `Jitter`, `BackoffCap`, `ExecutionTimeout`, `NonReentrant`, `FailuresBeforeBackoff` |

## ITaskManager

`ITaskManager` is the shared contract behind long-running workers and recurring jobs.

Typical flow:

1. schedule a worker or recurring job
2. keep the handle for cancellation or reporting
3. query active work through the manager

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

### 1.Common pitfalls

- scheduling recurring work and then dropping the handle immediately
- using worker names without a consistent naming convention
- forgetting to cancel or dispose long-lived work during shutdown

## IWorkerContext

`IWorkerContext` is passed into worker delegates so they can report heartbeat and progress.

## Example

```csharp
ctx.Beat();
ctx.Advance(5, "batch completed");
```

### 2.Common pitfalls`

- assuming the worker will update progress automatically
- forgetting to call `Beat()` inside long-running loops
- treating progress messages as optional if you want useful diagnostics

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

### 3.Common pitfalls

- reading handle state after the manager has already disposed it
- assuming a recurring job is still active just because the handle exists
- ignoring `Group` when you use group concurrency limits

## Options contracts

`IWorkerOptions` and `IRecurringOptions` describe the knobs the task manager uses to shape execution.

### 4.Common pitfalls

- leaving `Tag` empty when you rely on it for diagnostics or grouping
- setting `ExecutionTimeout` too low for a job that does real work
- assuming `NonReentrant` is optional if overlapping work would be harmful
- forgetting `BackoffCap` and `FailuresBeforeBackoff` are part of the retry shape for recurring jobs

## Related APIs

- [Task Manager](../framework/runtime/task-manager.md)
- [Worker Options](../framework/options/worker-options.md)
- [Recurring Options](../framework/options/recurring-options.md)
