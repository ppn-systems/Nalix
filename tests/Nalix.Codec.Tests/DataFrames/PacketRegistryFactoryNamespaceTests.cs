using Nalix.Abstractions.Serialization;
using System;
using System.IO;
using System.Reflection;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Codec.Serialization;
using Nalix.Codec.DataFrames;
using Xunit;

namespace Nalix.Codec.Tests.DataFrames
{
    public sealed class PacketRegistryFactoryNamespaceTests
    {
        private static readonly Assembly s_testAssembly = typeof(FactoryScan.FactoryScanPacket).Assembly;
        private const string RootNamespace = "Nalix.Codec.Tests.DataFrames.FactoryScan";

        [Fact]
        public void IncludeNamespaceWhenNamespaceIsNullThrowsArgumentNullException()
        {
            PacketRegistryFactory factory = new();

            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => factory.IncludeNamespace(null!));

            Assert.Equal("ns", ex.ParamName);
        }

        [Fact]
        public void IncludeNamespaceWhenNamespaceIsWhitespaceThrowsArgumentException()
        {
            PacketRegistryFactory factory = new();

            ArgumentException ex = Assert.Throws<ArgumentException>(() => factory.IncludeNamespace("   "));

            Assert.Equal("ns", ex.ParamName);
        }

        [Fact]
        public void IncludeNamespaceRecursiveWhenNamespaceIsNullThrowsArgumentNullException()
        {
            PacketRegistryFactory factory = new();

            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => factory.IncludeNamespaceRecursive(null!));

            Assert.Equal("rootNs", ex.ParamName);
        }

        [Fact]
        public void IncludeNamespaceRecursiveWhenNamespaceIsWhitespaceThrowsArgumentException()
        {
            PacketRegistryFactory factory = new();

            ArgumentException ex = Assert.Throws<ArgumentException>(() => factory.IncludeNamespaceRecursive(" "));

            Assert.Equal("rootNs", ex.ParamName);
        }

        [Fact]
        public void IncludeAssemblyWhenAssemblyIsNullReturnsSameFactoryInstance()
        {
            PacketRegistryFactory factory = new();

            Assert.Throws<ArgumentNullException>(() => factory.IncludeAssembly((Assembly)null!));
        }

        [Fact]
        public void RegisterAllPacketsWhenAssemblyIsNullReturnsSameFactoryInstance()
        {
            PacketRegistryFactory factory = new();

            Assert.Throws<ArgumentNullException>(() => factory.RegisterAllPackets(null!));
        }

        [Fact]
        public void RegisterAllPacketsWhenRequireAttributeIsFalseAndAssemblyContainsBrokenPacketThrowsInternalErrorException()
        {
            PacketRegistryFactory factory = new();
            _ = factory.RegisterAllPackets(s_testAssembly, requireAttribute: false);

            Exception ex = Assert.ThrowsAny<Exception>(factory.CreateCatalog);

            Assert.Contains("BrokenPacket", ex.Message, StringComparison.Ordinal);
            Assert.Contains("Deserialize", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void RegisterAllPacketsWhenRequireAttributeIsTrueIncludesOnlyAttributedPackets()
        {
            PacketRegistryFactory factory = new();
            _ = factory.RegisterAllPackets(s_testAssembly, requireAttribute: true);

            PacketRegistry registry = factory.CreateCatalog();

            Assert.False(registry.IsRegistered<FactoryScan.FactoryScanPacket>());
            Assert.False(registry.IsRegistered<FactoryScan.Child.FactoryScanChildPacket>());
            Assert.True(registry.IsRegistered<FactoryScan.FactoryScanAttributedPacket>());
        }

        [Fact]
        public void RegisterCurrentDomainPacketsWhenRequireAttributeIsTrueIncludesAttributedPacketsFromLoadedAssemblies()
        {
            PacketRegistryFactory factory = new();
            _ = factory.RegisterCurrentDomainPackets(requireAttribute: true);

            PacketRegistry registry = factory.CreateCatalog();

            Assert.True(registry.IsRegistered<FactoryScan.FactoryScanAttributedPacket>());
            Assert.False(registry.IsRegistered<FactoryScan.FactoryScanPacket>());
        }

        [Fact]
        public void RegisterPacketAssemblyWhenPathIsWhitespaceThrowsArgumentException()
        {
            PacketRegistryFactory factory = new();

            ArgumentException ex = Assert.Throws<ArgumentException>(() => factory.RegisterPacketAssembly(" "));

            Assert.Equal("assemblyPath", ex.ParamName);
        }

        [Fact]
        public void RegisterPacketAssemblyWhenPathDoesNotExistThrowsFileNotFoundException()
        {
            PacketRegistryFactory factory = new();

            _ = Assert.Throws<FileNotFoundException>(() => factory.RegisterPacketAssembly(@"Z:\this\path\does-not-exist\ghost.dll"));
        }

        [Fact]
        public void RegisterPacketAssemblyWhenRequireAttributeIsTrueIncludesOnlyAttributedPackets()
        {
            PacketRegistryFactory factory = new();
            _ = factory.RegisterPacketAssembly(s_testAssembly.Location, requireAttribute: true);

            PacketRegistry registry = factory.CreateCatalog();

            Assert.False(registry.IsRegistered<FactoryScan.FactoryScanPacket>());
            Assert.False(registry.IsRegistered<FactoryScan.Child.FactoryScanChildPacket>());
            Assert.True(registry.IsRegistered<FactoryScan.FactoryScanAttributedPacket>());
        }

        [Fact]
        public void IncludeNamespaceWhenUsingExactRootIncludesOnlyRootNamespacePackets()
        {
            PacketRegistryFactory factory = new();
            _ = factory.IncludeAssembly(s_testAssembly);
            _ = factory.IncludeNamespace(RootNamespace);

            PacketRegistry registry = factory.CreateCatalog();

            Assert.True(registry.IsRegistered<FactoryScan.FactoryScanPacket>());
            Assert.True(registry.IsRegistered<FactoryScan.FactoryScanAttributedPacket>());
            Assert.False(registry.IsRegistered<FactoryScan.Child.FactoryScanChildPacket>());
        }

        [Fact]
        public void IncludeNamespaceRecursiveWhenUsingRootIncludesRootAndChildNamespacePackets()
        {
            PacketRegistryFactory factory = new();
            _ = factory.IncludeAssembly(s_testAssembly);
            _ = factory.IncludeNamespaceRecursive(RootNamespace);

            PacketRegistry registry = factory.CreateCatalog();

            Assert.True(registry.IsRegistered<FactoryScan.FactoryScanPacket>());
            Assert.True(registry.IsRegistered<FactoryScan.FactoryScanAttributedPacket>());
            Assert.True(registry.IsRegistered<FactoryScan.Child.FactoryScanChildPacket>());
        }

        [Fact]
        public void LoadFromAssemblyPathWhenRequireAttributeIsTrueIncludesOnlyAttributedPackets()
        {
            PacketRegistry registry = new PacketRegistry(f => f.RegisterPacketAssembly(s_testAssembly.Location, requireAttribute: true));

            Assert.False(registry.IsRegistered<FactoryScan.FactoryScanPacket>());
            Assert.False(registry.IsRegistered<FactoryScan.Child.FactoryScanChildPacket>());
            Assert.True(registry.IsRegistered<FactoryScan.FactoryScanAttributedPacket>());
        }

        [Fact]
        public void LoadFromNamespaceWhenUsingCurrentDomainIncludesRootAndChildNamespacePackets()
        {
            PacketRegistry registry = new PacketRegistry(f => f.IncludeAssembly(s_testAssembly).IncludeNamespaceRecursive(RootNamespace));

            Assert.True(registry.IsRegistered<FactoryScan.FactoryScanPacket>());
            Assert.True(registry.IsRegistered<FactoryScan.FactoryScanAttributedPacket>());
            Assert.True(registry.IsRegistered<FactoryScan.Child.FactoryScanChildPacket>());
        }

        [Fact]
        public void LoadFromNamespaceWhenAssemblyPathProvidedIncludesOnlyRequestedNamespace()
        {
            PacketRegistry registry = new PacketRegistry(f => f.IncludeAssembly(s_testAssembly).IncludeNamespace(RootNamespace));

            Assert.True(registry.IsRegistered<FactoryScan.FactoryScanPacket>());
            Assert.True(registry.IsRegistered<FactoryScan.FactoryScanAttributedPacket>());
            Assert.False(registry.IsRegistered<FactoryScan.Child.FactoryScanChildPacket>());
        }
    }
}

namespace Nalix.Codec.Tests.DataFrames.FactoryScan
{
    public sealed class FactoryScanPacket : PacketBase<FactoryScanPacket>
    {
        [SerializeOrder(PacketHeaderOffset.Region)]
        public ushort Value { get; set; }

        public static new FactoryScanPacket Deserialize(ReadOnlySpan<byte> buffer)
            => PacketBase<FactoryScanPacket>.Deserialize(buffer);
    }

    [Packet]
    public sealed class FactoryScanAttributedPacket : PacketBase<FactoryScanAttributedPacket>
    {
        [SerializeOrder(PacketHeaderOffset.Region)]
        public ushort Value { get; set; }

        public static new FactoryScanAttributedPacket Deserialize(ReadOnlySpan<byte> buffer)
            => PacketBase<FactoryScanAttributedPacket>.Deserialize(buffer);
    }
}

namespace Nalix.Codec.Tests.DataFrames.FactoryScan.Child
{
    public sealed class FactoryScanChildPacket : PacketBase<FactoryScanChildPacket>
    {
        [SerializeOrder(PacketHeaderOffset.Region)]
        public ushort Value { get; set; }

        public static new FactoryScanChildPacket Deserialize(ReadOnlySpan<byte> buffer)
            => PacketBase<FactoryScanChildPacket>.Deserialize(buffer);
    }
}

















