using System;
using System.Reflection;
using FluentAssertions;
using Xunit;

namespace Nalix.Network.Tests;

public sealed class SlidingWindowTests
{
    private static readonly Type s_slidingWindowType = ResolveSlidingWindowType();
    private static readonly MethodInfo s_tryCheckMethod =
        s_slidingWindowType.GetMethod("TryCheck", [typeof(ushort)])
        ?? throw new InvalidOperationException("Unable to resolve SlidingWindow.TryCheck(ushort).");

    [Fact]
    public void TryCheck_WrapAround_65535To0_AcceptsNewPackets()
    {
        object window = CreateWindow(windowSize: 1024);

        InvokeTryCheck(window, 65534).Should().BeTrue();
        InvokeTryCheck(window, 65535).Should().BeTrue();
        InvokeTryCheck(window, 0).Should().BeTrue();
        InvokeTryCheck(window, 1).Should().BeTrue();
    }

    [Fact]
    public void TryCheck_WrapAround_ReplayedPackets_AreRejected()
    {
        object window = CreateWindow(windowSize: 1024);

        InvokeTryCheck(window, 65535).Should().BeTrue();
        InvokeTryCheck(window, 0).Should().BeTrue();

        InvokeTryCheck(window, 65535).Should().BeFalse();
        InvokeTryCheck(window, 0).Should().BeFalse();
    }

    [Fact]
    public void TryCheck_WrapAround_OutOfOrderWithinWindow_AcceptsPacket()
    {
        object window = CreateWindow(windowSize: 1024);

        InvokeTryCheck(window, 65534).Should().BeTrue();
        InvokeTryCheck(window, 0).Should().BeTrue();
        InvokeTryCheck(window, 65535).Should().BeTrue();
    }

    private static object CreateWindow(int windowSize)
        => Activator.CreateInstance(s_slidingWindowType, [windowSize])
           ?? throw new InvalidOperationException("Unable to instantiate SlidingWindow.");

    private static bool InvokeTryCheck(object window, ushort seq)
        => (bool)(s_tryCheckMethod.Invoke(window, [seq])
                  ?? throw new InvalidOperationException("SlidingWindow.TryCheck returned null."));

    private static Type ResolveSlidingWindowType()
        => Type.GetType("Nalix.Network.Internal.Security.SlidingWindow, Nalix.Network")
           ?? throw new InvalidOperationException("Unable to resolve Nalix.Network.Internal.Security.SlidingWindow.");
}













