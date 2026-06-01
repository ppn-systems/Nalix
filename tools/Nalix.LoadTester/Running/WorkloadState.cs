// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.LoadTester.Running;

internal sealed class WorkloadState
{
    private int _phase = (int)WorkloadPhase.RampUp;
    private int _activeWorkers;

    public WorkloadPhase Phase
    {
        get => (WorkloadPhase)Volatile.Read(ref _phase);
        set => Volatile.Write(ref _phase, (int)value);
    }

    public int ActiveWorkers => Volatile.Read(ref _activeWorkers);

    public void WorkerStarted() => Interlocked.Increment(ref _activeWorkers);

    public void WorkerStopped() => Interlocked.Decrement(ref _activeWorkers);
}
