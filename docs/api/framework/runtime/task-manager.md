# TaskManager

`TaskManager` schedules workers and recurring jobs with group gating, cancellation, and diagnostics.

## Source mapping

- `src/Nalix.Framework/Tasks/TaskManager.cs`
- `src/Nalix.Framework/Tasks/TaskManager.Names.cs`
- `src/Nalix.Framework/Tasks/TaskManager.PrivateMethods.cs`
- `src/Nalix.Framework/Tasks/TaskManager.State.cs`
- `src/Nalix.Framework/Options/TaskManagerOptions.cs`
- `src/Nalix.Framework/Options/WorkerOptions.cs`
- `src/Nalix.Framework/Options/RecurringOptions.cs`

## Main types

- `TaskManager`
- `TaskNaming`

## What it does

- schedules one-off workers
- schedules recurring jobs
- supports group-level concurrency gates
- supports cancellation by worker ID, group, or recurring name
- produces text and structured diagnostic reports

## Construction

```csharp
TaskManager manager = new();
TaskManager custom = new(new TaskManagerOptions { MaxWorkers = 20 });
```

The parameterless constructor loads `TaskManagerOptions` from `ConfigurationManager`.

## Core APIs

| Method | Purpose |
| --- | --- |
| `ScheduleWorker(...)` | Start a one-off worker task and receive an `IWorkerHandle`. |
| `ScheduleRecurring(...)` | Start a recurring job and receive an `IRecurringHandle`. |
| `RunOnceAsync(...)` | Run a one-off async operation without registering a worker. |
| `CancelAllWorkers()` | Cancel every active worker. |
| `CancelWorker(ISnowflake)` | Cancel one worker by ID. |
| `CancelGroup(string)` | Cancel all workers in one group. |
| `CancelRecurring(string?)` | Cancel one recurring job by name. |
| `GetWorkers(...)` | Read back worker handles, optionally filtered. |
| `GetRecurring()` | Read back recurring handles. |
| `TryGetRecurring(...)` | Try to read one recurring job by name. |
| `GenerateReport()` | Return a text snapshot of runtime state. |
| `GenerateReportData()` | Return a machine-readable diagnostics snapshot. |

## Worker scheduling

`ScheduleWorker(...)` accepts:

- `name`
- `group`
- `Func<IWorkerContext, CancellationToken, ValueTask>` work delegate
- optional `IWorkerOptions`

The options type is `WorkerOptions`, and the most relevant properties are:

- `Tag`
- `MachineId`
- `IdType`
- `ExecutionTimeout`
- `RetainFor`
- `GroupConcurrencyLimit`
- `TryAcquireSlotImmediately`
- `OnCompleted`
- `OnFailed`

## Recurring scheduling

`ScheduleRecurring(...)` accepts:

- `name`
- `interval`
- `Func<CancellationToken, ValueTask>` work delegate
- optional `IRecurringOptions`

The options type is `RecurringOptions`, and the most relevant properties are:

- `NonReentrant`
- `Jitter`
- `ExecutionTimeout`
- `FailuresBeforeBackoff`
- `BackoffCap`
- `Tag`

## Task naming

`TaskNaming` provides canonical naming helpers.

Useful members include:

- `TaskNaming.Tags`
- `TaskNaming.Recurring.CleanupJobId(...)`
- `TaskNaming.SanitizeToken(...)`

## Diagnostics

`GenerateReport()` returns a detailed text report covering:

- process health
- worker and recurring statistics
- group concurrency usage
- top running workers

`GenerateReportData()` returns the same state as a dictionary for programmatic consumption.

## Basic usage

```csharp
TaskManager manager = new();

IWorkerHandle worker = manager.ScheduleWorker(
    "session.cleanup",
    "session",
    async (ctx, ct) =>
    {
        await Task.Yield();
    });

IRecurringHandle recurring = manager.ScheduleRecurring(
    "heartbeat",
    TimeSpan.FromSeconds(10),
    async ct => await Task.Yield());
```

## Related APIs

- [Configuration and DI](./configuration.md)
- [Worker Options](../options/worker-options.md)
- [Recurring Options](../options/recurring-options.md)
