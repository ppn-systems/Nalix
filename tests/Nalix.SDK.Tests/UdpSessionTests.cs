using System.Reflection;
using Nalix.Environment.Memory;
using Nalix.SDK.Options;
using Nalix.SDK.Transport;

namespace Nalix.SDK.Tests;

public sealed class UdpSessionTests
{
    [Fact]
    public async Task OnMessageAsyncSubscribedAfterConstructionReceivesUdpMessages()
    {
        using UdpSession session = new(new TransportOptions());
        int calls = 0;
        session.OnMessageAsync += _ =>
        {
            calls++;
            return Task.CompletedTask;
        };

        object reader = typeof(UdpSession)
            .GetField("_reader", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(session)!;

        MethodInfo dispatch = reader.GetType()
            .GetMethod("DispatchMessageAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

        using BufferLease lease = BufferLease.CopyFrom([1, 2, 3, 4]);
        await (Task)dispatch.Invoke(reader, [lease, CancellationToken.None])!;

        Assert.Equal(1, calls);
    }
}
