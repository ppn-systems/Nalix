using System;
using System.Collections.Generic;
using Nalix.Framework.DataFrames;
using Xunit;

namespace Nalix.Framework.Tests.DataFrames;

public sealed class PacketRegisterTests
{
    [Fact]
    public void CreateCatalogFromCurrentDomainWhenRequirePacketAttributeIsTrueIncludesAttributedPacket()
    {
        PacketRegistry registry = PacketRegister.CreateCatalogFromCurrentDomain(requirePacketAttribute: true);

        Assert.True(registry.IsRegistered<FactoryScan.FactoryScanAttributedPacket>());
        Assert.False(registry.IsRegistered<FactoryScan.FactoryScanPacket>());
    }

    [Fact]
    public void CreateCatalogFromAssemblyPathWhenRequirePacketAttributeIsTrueIncludesOnlyAttributedPacket()
    {
        string assemblyPath = typeof(FactoryScan.FactoryScanPacket).Assembly.Location;

        PacketRegistry registry = PacketRegister.CreateCatalogFromAssemblyPath(assemblyPath, requirePacketAttribute: true);

        Assert.True(registry.IsRegistered<FactoryScan.FactoryScanAttributedPacket>());
        Assert.False(registry.IsRegistered<FactoryScan.FactoryScanPacket>());
    }

    [Fact]
    public void CreateCatalogFromAssembliesWhenInputContainsNullThrowsArgumentException()
    {
        List<System.Reflection.Assembly> assemblies = [typeof(FactoryScan.FactoryScanPacket).Assembly, null!];

        ArgumentException ex = Assert.Throws<ArgumentException>(() => PacketRegister.CreateCatalogFromAssemblies(assemblies));

        Assert.Equal("assemblies", ex.ParamName);
    }

    [Fact]
    public void CreateCatalogFromAssemblyPathsWhenInputContainsWhitespaceThrowsArgumentException()
    {
        List<string> assemblyPaths = [typeof(FactoryScan.FactoryScanPacket).Assembly.Location, " "];

        ArgumentException ex = Assert.Throws<ArgumentException>(() => PacketRegister.CreateCatalogFromAssemblyPaths(assemblyPaths));

        Assert.Equal("assemblyPaths", ex.ParamName);
    }
}
