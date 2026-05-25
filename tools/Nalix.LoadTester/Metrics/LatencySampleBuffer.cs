// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.LoadTester.Metrics;

internal sealed class LatencySampleBuffer
{
    private readonly Double[] _samples;
    private Int64 _sampleIndex;

    public LatencySampleBuffer(Int32 capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _samples = new Double[capacity];
    }

    public void Add(Double latencyMs)
    {
        Int64 index = Interlocked.Increment(ref _sampleIndex) - 1;
        _samples[(Int32)(index % _samples.Length)] = latencyMs;
    }

    public void Reset()
    {
        Volatile.Write(ref _sampleIndex, 0);
        Array.Clear(_samples);
    }

    public Double[] Snapshot(out Int64 count)
    {
        count = Math.Min(Volatile.Read(ref _sampleIndex), _samples.Length);
        if (count == 0)
        {
            return [];
        }

        Int32 length = (Int32)count;
        Double[] activeSamples = new Double[length];
        Array.Copy(_samples, 0, activeSamples, 0, length);
        Array.Sort(activeSamples);
        return activeSamples;
    }
}
