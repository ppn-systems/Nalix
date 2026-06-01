// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.LoadTester.Metrics;

internal sealed class LatencySampleBuffer
{
    private readonly double[] _samples;
    private long _sampleIndex;

    public LatencySampleBuffer(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _samples = new double[capacity];
    }

    public void Add(double latencyMs)
    {
        long index = Interlocked.Increment(ref _sampleIndex) - 1;
        _samples[(int)(index % _samples.Length)] = latencyMs;
    }

    public void Reset()
    {
        Volatile.Write(ref _sampleIndex, 0);
        Array.Clear(_samples);
    }

    public double[] Snapshot(out long count)
    {
        count = Math.Min(Volatile.Read(ref _sampleIndex), _samples.Length);
        if (count == 0)
        {
            return [];
        }

        int length = (int)count;
        double[] activeSamples = new double[length];
        Array.Copy(_samples, 0, activeSamples, 0, length);
        Array.Sort(activeSamples);
        return activeSamples;
    }
}
