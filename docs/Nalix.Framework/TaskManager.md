# TaskManager — Powerful Background Task & Scheduling Manager

**TaskManager** provides robust scheduling for background workers and recurring tasks — with advanced features like group limiting, dynamic concurrency, diagnostics, and sophisticated error/backoff handling.  
It is ideal for distributed background processing, regular polling, periodic health checks, or complex ETL jobs.

- **Namespace:** `Nalix.Framework.Tasks`
- **Class:** `TaskManager`
- **Configurable via:** `TaskManagerOptions`, `WorkerOptions`, `RecurringOptions` (set directly or via `ConfigurationManager`)

---

## Features

- **Schedule one-off, recurring, or group-limited tasks**
- **Dynamic worker concurrency** with automatic CPU/load monitoring and throttling
- **Built-in metrics:** execution time, error rates, task progress, reporting
- **Full diagnostics and cancellation APIs**
- **All operations thread-safe**

---

## Basic Usage

### Instantiate the TaskManager

```csharp
using Nalix.Framework.Tasks;

// Default options (from ConfigurationManager), or pass custom options
TaskManager taskManager = new TaskManager();
// Or with custom options
TaskManager taskManager = new TaskManager(new TaskManagerOptions { MaxWorkers = 20 });
```

---

### Schedule a One-off Worker

```csharp
using Nalix.Framework.Options;

// Start a worker (with group and options)
IWorkerHandle handle = taskManager.ScheduleWorker(
    name: "data.import",
    group: "etl",
    async (ctx, ct) => {
        // ... worker logic here
        await SomeLongOperationAsync(ct);
        ctx.Advance(1, "step done");    // Report progress
    },
    new WorkerOptions
    {
        Tag = "import",
        ExecutionTimeout = TimeSpan.FromMinutes(1)
    }
);

// You can cancel/halt worker by handle:
handle.Cancel();
```

---

### Schedule a Recurring Task

```csharp
using Nalix.Framework.Options;

// Schedule a recurring task every 10 seconds
IRecurringHandle recurringHandle = taskManager.ScheduleRecurring(
    name: "heartbeat",
    interval: TimeSpan.FromSeconds(10),
    async ct => { /* periodic logic here */ },
    new RecurringOptions
    {
        ExecutionTimeout = TimeSpan.FromSeconds(4),
        Jitter = TimeSpan.FromMilliseconds(500),
        NonReentrant = true
    }
);

// Later, to stop:
taskManager.CancelRecurring("heartbeat");
```

---

## API Overview

| Method                                                              | Description                                  |
|---------------------------------------------------------------------|----------------------------------------------|
| `ScheduleWorker(name, group, work, options?)`                       | Schedule a one-off worker task               |
| `ScheduleRecurring(name, interval, work, options?)`                 | Schedule a periodic/recurring background task|
| `RunOnceAsync(name, work, ct)`                                      | Run a fire-and-forget async task once        |
| `CancelAllWorkers()`                                                | Cancel all active workers                    |
| `CancelWorker(id)`                                                  | Cancel a specific worker by ID               |
| `CancelGroup(group)`                                                | Cancel all workers by group                  |
| `CancelRecurring(name)`                                             | Cancel (stop) a recurring task               |
| `GetWorkers(runningOnly, group?)`                                   | Get all (or running) worker handles          |
| `GetRecurring()`                                                    | List all active recurring handles            |
| `GenerateReport()`                                                  | Full runtime status and metrics report       |

---

## Key Options

### TaskManagerOptions (global)

| Property                  | Type          | Meaning                                                |
|---------------------------|---------------|--------------------------------------------------------|
| `MaxWorkers`              | int           | Global worker concurrency limit (default: 100)         |
| `DynamicAdjustmentEnabled`| bool          | Adaptive concurrency based on CPU load (default: true) |
| `CleanupInterval`         | TimeSpan      | Worker cleanup frequency (default: 30s)                |
| `IsEnableLatency`         | bool          | Collect/track latency & timing info (default: true)    |

---

### WorkerOptions

| Property                    | Type         | Meaning                                      |
|-----------------------------|--------------|----------------------------------------------|
| `Tag`                       | string?      | Custom tag for diagnostics                   |
| `MachineId`                 | ushort       | Node/machine identity                        |
| `IdType`                    | enum         | Worker type, for system audits               |
| `ExecutionTimeout`          | TimeSpan?    | Auto-cancel if exceeded                      |
| `RetainFor`                 | TimeSpan?    | How long to keep worker data after finish    |
| `GroupConcurrencyLimit`     | int?         | Max concurrent tasks for a group             |
| `TryAcquireSlotImmediately` | bool         | If true, fail immediately if group saturated |
| `OnCompleted`               | Action       | Handler callback on success                  |
| `OnFailed`                  | Action       | Handler callback on error                    |

---

### RecurringOptions

| Property                 | Type         | Meaning                             |
|--------------------------|--------------|-------------------------------------|
| `NonReentrant`           | bool         | Prevent overlapping invocations     |
| `Jitter`                 | TimeSpan?    | Randomize initial delay (def: 250ms)|
| `ExecutionTimeout`       | TimeSpan?    | Soft time budget/cancel per run     |
| `FailuresBeforeBackoff`  | int          | Max fails before backoff            |
| `BackoffCap`             | TimeSpan     | Max backoff delay after errors      |
| `Tag`                    | string?      | Diagnostic tag                      |

---

## Accessing Diagnostics & Reporting

**Generate a live snapshot of all workers and recurring tasks:**

```csharp
string report = taskManager.GenerateReport();
Console.WriteLine(report);
```

---

## Cancellation

- **By worker ID:**  

  ```csharp
  taskManager.CancelWorker(workerId);
  ```

- **By group:**  

  ```csharp
  taskManager.CancelGroup("etl");
  ```

- **All workers:**  

  ```csharp
  taskManager.CancelAllWorkers();
  ```

- **Recurring task:**  

  ```csharp
  taskManager.CancelRecurring("heartbeat");
  ```

---

## Querying Task Status

**Worker handles** report progress, running/completed state, last note, start time:

```csharp
foreach (IReadOnlyCollection<IWorkerHandle> worker in taskManager.GetWorkers(runningOnly: true))
{
    Console.WriteLine($"{worker.Name} ({worker.Group}): Running={worker.IsRunning} Progress={worker.Progress}");
}
```

**Recurring handles** report last/next run, total errors, etc.

---

## Best Practices

- Use **group names** to organize/constrain similar jobs (ETL, pollers, etc).
- Leverage **Jitter** and **NonReentrant** for safe, distributed scheduling.
- Always set **ExecutionTimeout** in noisy/long operations to avoid runaway tasks.
- Use **Tag** property for easy search/diagnostics of workers.
- Clean up with **Dispose()** or use in a `using` block for graceful shutdown.

---

## Example: Complex Scheduling

```csharp
TaskManager tm = new TaskManager();

tm.ScheduleRecurring(
    "poll.weather", TimeSpan.FromSeconds(30),
    async ct => {
        // Polling logic here
    },
    new RecurringOptions { NonReentrant = true, Jitter = TimeSpan.FromMilliseconds(400) }
);

tm.ScheduleWorker(
    "bulk.process", "analytics",
    async (ctx, ct) => {
        // Heavy processing
        ctx.Advance(10, "started step 1");
        // ...
    },
    new WorkerOptions { GroupConcurrencyLimit = 2 }
);

// Live console diagnostics
Console.WriteLine(tm.GenerateReport());
```

---

## Notes

- All APIs are thread-safe and robust for use in modern .NET apps.
- Concurrency limits and adaptive throttling make this manager scalable for cloud/server tasks.
- Configuration can be set in code or by `ConfigurationManager` from ini/json.

---

## License

Licensed under the Apache License, Version 2.0.
