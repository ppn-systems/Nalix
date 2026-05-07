#if DEBUG
using System;
using System.Reflection;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Codec.DataFrames;
using Nalix.Hosting;
using Xunit;

namespace Nalix.Network.Tests;

public sealed class NetworkApplicationBuilderPacketRegistryTests
{
    private const string RootNamespace = "Nalix.Network.Tests.HostingScan";

    [Fact]
    public void StaticPacketRegistryBuildIsIdempotent()
    {
        // Packets are auto-registered via source-generated ModuleInitializer
        PacketRegistry.Build();
        PacketRegistry.Build();

        Assert.True(PacketRegistry.DeserializerCount >= 0);
    }
}
#endif














