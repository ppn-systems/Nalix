// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Threading;
using Nalix.Framework.Time;
using Xunit;

namespace Nalix.Framework.Tests.Time;

public sealed class TimingScopeTests
{
    [Fact]
    public void TimingScopeWhenStartedAndElapsedReturnsPositiveTicksAndMilliseconds()
    {
        TimingScope scope = TimingScope.Start();
        Thread.Sleep(5);

        long ticks = scope.ElapsedTicks;
        double milliseconds = scope.GetElapsedMilliseconds();

        Assert.True(ticks > 0);
        Assert.True(milliseconds >= 0);
    }

    [Fact]
    public void TimingScopeWhenReadMultipleTimesElapsedMillisecondsIsNonDecreasing()
    {
        TimingScope scope = TimingScope.Start();
        Thread.Sleep(2);
        double first = scope.GetElapsedMilliseconds();
        Thread.Sleep(2);
        double second = scope.GetElapsedMilliseconds();

        Assert.True(second >= first);
    }
}
