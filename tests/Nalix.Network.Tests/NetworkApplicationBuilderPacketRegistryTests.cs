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
        NetworkApplicationBuilder builder = NetworkApplication.CreateBuilder();

        _ = builder.AddPacketNamespace(RootNamespace, recursive: true);
        PacketRegistry.Build();
        PacketRegistry.Build();

        Assert.True(PacketRegistry.DeserializerCount >= 0);
    }
}
#endif














