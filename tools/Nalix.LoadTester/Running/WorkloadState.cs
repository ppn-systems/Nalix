// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.LoadTester.Running;

internal sealed class WorkloadState
{
    private Int32 _phase = (Int32)WorkloadPhase.RampUp;
    private Int32 _activeWorkers;

    public WorkloadPhase Phase
    {
        get => (WorkloadPhase)Volatile.Read(ref _phase);
        set => Volatile.Write(ref _phase, (Int32)value);
    }

    public Int32 ActiveWorkers => Volatile.Read(ref _activeWorkers);

    public void WorkerStarted() => Interlocked.Increment(ref _activeWorkers);

    public void WorkerStopped() => Interlocked.Decrement(ref _activeWorkers);
}
