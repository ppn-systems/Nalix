# TaskManager Documentation

## Overview

`TaskManager` is a robust class for managing background tasks in .NET applications. It supports two main types of tasks: recurring (scheduled, repeated) tasks and workers (background jobs, either single-run or long-running). The manager is fully thread-safe, supports automatic cleanup of finished workers, task cancellation, group concurrency control, detailed reporting, and is designed for high efficiency and maintainability.

---

## Table of Contents

- [Overview](#overview)
- [Detailed Code Explanation](#detailed-code-explanation)
  - [Fields & Structure](#fields--structure)
  - [Main APIs](#main-apis)
  - [State Tracking & Reporting](#state-tracking--reporting)
  - [Automatic Cleanup & Internal Mechanics](#automatic-cleanup--internal-mechanics)
  - [Task & Worker Options](#task--worker-options)
- [Usage](#usage)
- [Examples](#examples)
- [Notes & Security](#notes--security)
- [SOLID & DDD Principles](#solid--ddd-principles)

---

## Detailed Code Explanation

### Fields & Structure

- Uses thread-safe collections (`ConcurrentDictionary`) to store workers, recurring tasks, and group gates.
- Each worker/recurring task has its own state class (e.g., `WorkerState`, `RecurringState`).
- Internal timer (`_cleanupTimer`) periodically checks and removes finished workers to conserve memory.
- Uses `CancellationTokenSource` for cancellation support, ensuring safe task termination.

---

### Main APIs

#### Recurring Task Management

- `ScheduleRecurring(...)`: Schedule a recurring background task with a specified interval.
- `CancelRecurring(name)`: Cancel a recurring task by its unique name.
- `TryGetRecurring(name, out handle)`: Try to retrieve a recurring task handle by name.
- `ListRecurring()`: List all scheduled recurring tasks.

#### Worker Management

- `StartWorker(name, group, work, options)`: Start a new worker task in the background, with optional group-based concurrency limit.
- `CancelWorker(id)`: Cancel a worker by its unique identifier.
- `CancelGroup(group)`: Cancel all workers in a specific group.
- `CancelAllWorkers()`: Cancel all running workers.
- `TryGetWorker(id, out handle)`: Try to retrieve a worker handle by identifier.
- `ListWorkers(runningOnly, group)`: List all workers, with optional filters for running status and group.

#### Single-Run Job

- `RunSingleJob(name, work, ct)`: Run a single background job with cancellation support.

#### Reporting

- `GenerateReport()`: Generate a detailed report of all recurring tasks and workers, grouped and summarized.

---

### State Tracking & Reporting

- **WorkerState**: Tracks execution status, statistics, timestamps, progress, and notes for each worker.
- **RecurringState**: Tracks scheduled execution, run statistics, failure counts, and timing for recurring tasks.
- **WorkerContext**: Passed into worker delegates, lets you update progress, check for cancellation, and record activity ("heartbeat").

---

### Automatic Cleanup & Internal Mechanics

- Finished workers are automatically removed from memory after a configurable retention period.
- Group gates (using `SemaphoreSlim`) are disposed and removed if no workers remain in the group.
- Recurring tasks use backoff and jitter logic to handle errors and avoid scheduling spikes.
- All resource handles (timers, semaphores, CancellationTokenSource) are properly disposed to avoid leaks.

---

### Task & Worker Options

- **RecurringOptions**:
  - `Tag`, `NonReentrant`, `Jitter`, `RunTimeout`, `MaxFailuresBeforeBackoff`, `MaxBackoff`.
- **WorkerOptions**:
  - `Tag`, `MachineId`, `IdType`, `Retention`, `MaxGroupConcurrency`, `TryAcquireGroupSlotImmediately`, `CancellationToken`, `OnCompleted`, `OnFailed`.

---

## Usage

### 1. Schedule a recurring task

```csharp
var manager = new TaskManager();
manager.ScheduleRecurring(
    "data-sync",
    TimeSpan.FromMinutes(5),
    async ct => { await SyncDataAsync(ct); },
    new RecurringOptions { Tag = "sync", Jitter = TimeSpan.FromSeconds(5) }
);
```

### 2. Start a worker

```csharp
var workerHandle = manager.StartWorker(
    "file-upload",
    "upload-group",
    async (ctx, ct) => {
        // ... file upload logic
        ctx.Advance(20, "Uploaded chunk 1");
        await Task.Delay(1000, ct);
        ctx.Advance(80, "Uploaded chunk 2");
    },
    new WorkerOptions { Tag = "uploader", MaxGroupConcurrency = 3 }
);
```

### 3. Cancel a worker or recurring task

```csharp
bool cancelled = manager.CancelWorker(workerHandle.Id);
// Or
manager.CancelRecurring("data-sync");
```

### 4. Generate a status report

```csharp
string report = manager.GenerateReport();
Console.WriteLine(report);
```

### 5. Dispose resources

```csharp
manager.Dispose();
```

---

## Examples

```csharp
using Nalix.Framework.Tasks;
using Nalix.Framework.Tasks.Options;

var manager = new TaskManager();

// Schedule a recurring task every 10 minutes
manager.ScheduleRecurring(
    "heartbeat",
    TimeSpan.FromMinutes(10),
    async ct =>
    {
        // Send heartbeat logic
        await Task.Delay(300, ct);
    }
);

// Start a file upload worker
var handle = manager.StartWorker(
    "upload",
    "file-group",
    async (ctx, ct) =>
    {
        for (int i = 1; i <= 5; i++)
        {
            ctx.Advance(20, $"Chunk {i}/5");
            await Task.Delay(500, ct);
        }
    },
    new WorkerOptions { MaxGroupConcurrency = 2 }
);

// Cancel a worker if needed
manager.CancelWorker(handle.Id);

// Print a report of all tasks
Console.WriteLine(manager.GenerateReport());

// Clean up when finished
manager.Dispose();
```

---

## Notes & Security

- **Thread Safety:** All collections and state are thread-safe (`ConcurrentDictionary`, atomic variables, `SemaphoreSlim`).
- **Resource Limits:** Always set an appropriate retention time for workers to prevent memory leaks; recurring tasks back off after repeated failures.
- **Proper Disposal:** Always call `Dispose()` to release timers, CancellationTokenSource, and semaphores.
- **Logging:** Integrate `ILogger` for full visibility on errors, timeouts, and concurrency rejections.
- **No Resource Leaks:** All handles (Task, Cts, Gate) are disposed when no longer needed.
- **Avoid Deadlocks:** Do not use blocking calls inside task callbacks.
- **Exception Handling:** All exceptions are logged, and `OnFailed` callbacks are invoked as appropriate.

---

## SOLID & DDD Principles

- **SRP:** `TaskManager` is responsible only for task lifecycle and state management.
- **OCP:** Easily extendable for new task types or behaviors via options and interfaces.
- **LSP:** All handles implement interfaces (e.g. `IWorkerHandle`, `IRecurringHandle`) for easy substitution and mocking.
- **ISP:** Separate interfaces for each task/job type improve maintainability and clarity.
- **DIP:** Depends on abstractions (e.g. `ILogger`, `IIdentifier`, `IWorkerHandle`) for easier testing and extension.

**Domain-Driven Design (DDD):**  

- Workers and recurring tasks are managed as value objects with immutable state outside their lifecycle.
- Suitable for use as aggregate roots or within bounded contexts.

---

## Additional Notes

- Designed for modern .NET (C#), leveraging features like records, `init`, cancellation tokens, and `async/await`.
- Can be integrated into ASP.NET Core, Worker Services, or any .NET app needing robust background task orchestration.
- Well-suited for distributed systems, multi-process applications, or any scenario with complex background processing.

---
