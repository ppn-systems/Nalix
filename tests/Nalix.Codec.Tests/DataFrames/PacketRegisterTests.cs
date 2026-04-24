using System;
using System.Collections.Generic;
using Nalix.Codec.DataFrames;
using Xunit;

namespace Nalix.Codec.Tests.DataFrames;

public sealed class PacketRegisterTests
{
    [Fact]
    public void CreateCatalogFromCurrentDomainWhenRequirePacketAttributeIsTrueIncludesAttributedPacket()
    {
        PacketRegistry registry = new PacketRegistry(f => f.RegisterCurrentDomainPackets(requireAttribute: true));

        Assert.True(registry.IsRegistered<FactoryScan.FactoryScanAttributedPacket>());
        Assert.False(registry.IsRegistered<FactoryScan.FactoryScanPacket>());
    }

    [Fact]
    public void CreateCatalogFromAssemblyPathWhenRequirePacketAttributeIsTrueIncludesOnlyAttributedPacket()
    {
        string assemblyPath = typeof(FactoryScan.FactoryScanPacket).Assembly.Location;

        PacketRegistry registry = new PacketRegistry(f => f.RegisterPacketAssembly(assemblyPath, requireAttribute: true));

        Assert.True(registry.IsRegistered<FactoryScan.FactoryScanAttributedPacket>());
        Assert.False(registry.IsRegistered<FactoryScan.FactoryScanPacket>());
    }

    [Fact]
    public void CreateCatalogFromAssembliesWhenInputContainsNullThrowsArgumentException()
    {
        List<System.Reflection.Assembly> assemblies = [typeof(FactoryScan.FactoryScanPacket).Assembly, null!];

        ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => new PacketRegistry(f => { foreach(var a in assemblies) f.IncludeAssembly(a); }));

        Assert.Equal("asm", ex.ParamName);
    }

    [Fact]
    public void CreateCatalogFromAssemblyPathsWhenInputContainsWhitespaceThrowsArgumentException()
    {
        List<string> assemblyPaths = [typeof(FactoryScan.FactoryScanPacket).Assembly.Location, " "];

        ArgumentException ex = Assert.Throws<ArgumentException>(() => new PacketRegistry(f => { foreach(var p in assemblyPaths) f.RegisterPacketAssembly(p); }));

        Assert.Equal("assemblyPath", ex.ParamName);
    }
}
















