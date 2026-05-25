// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.LoadTester.Running;

internal sealed class WorkerHandle : IDisposable
{
    private readonly CancellationTokenSource _cancellation;
    private Int32 _disposed;

    public WorkerHandle(Task task, CancellationTokenSource cancellation)
    {
        this.Task = task ?? throw new ArgumentNullException(nameof(task));
        _cancellation = cancellation ?? throw new ArgumentNullException(nameof(cancellation));
    }

    public Task Task { get; }

    public void Stop()
    {
        if (!_cancellation.IsCancellationRequested)
        {
            _cancellation.Cancel();
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _cancellation.Dispose();
        }
    }
}
