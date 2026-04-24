using System;
using System.Reflection;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Serialization;
using Nalix.Framework.DataFrames;
using Nalix.Network.Hosting;
using Xunit;

namespace Nalix.Network.Test;

public sealed class NetworkApplicationBuilderPacketRegistryTests
{
    private const string RootNamespace = "Nalix.Network.Test.HostingScan";

    [Fact]
    public void ConfigurePacketRegistryWhenProvidedSkipsAutomaticPacketRegistration()
    {
        NetworkApplicationBuilder builder = NetworkApplication.CreateBuilder();
        PacketRegistry provided = PacketRegistry.LoadFromAssemblyPath(typeof(HostingScan.HostingScanAttributedPacket).Assembly.Location, requirePacketAttribute: true);

        _ = builder.ConfigurePacketRegistry(provided);
        _ = builder.AddPacketNamespace(RootNamespace, recursive: true);

        IPacketRegistry resolved = CreateRegistry(builder);

        Assert.Same(provided, resolved);
        Assert.True(resolved.IsRegistered<HostingScan.HostingScanAttributedPacket>());
        Assert.False(resolved.IsRegistered<HostingScan.HostingScanPacket>());
    }

    [Fact]
    public void AddPacketWhenAssemblyPathAndRequirePacketAttributeTrueIncludesOnlyAttributedPackets()
    {
        NetworkApplicationBuilder builder = NetworkApplication.CreateBuilder();
        _ = builder.AddPacket(typeof(HostingScan.HostingScanPacket).Assembly.Location, requirePacketAttribute: true);

        IPacketRegistry resolved = CreateRegistry(builder);

        Assert.True(resolved.IsRegistered<HostingScan.HostingScanAttributedPacket>());
        Assert.False(resolved.IsRegistered<HostingScan.HostingScanPacket>());
        Assert.False(resolved.IsRegistered<HostingScan.Child.HostingScanChildPacket>());
    }

    [Fact]
    public void AddPacketNamespaceWhenRecursiveIsFalseIncludesOnlyExactNamespacePackets()
    {
        NetworkApplicationBuilder builder = NetworkApplication.CreateBuilder();
        _ = builder.AddPacketNamespace(RootNamespace, recursive: false);

        IPacketRegistry resolved = CreateRegistry(builder);

        Assert.True(resolved.IsRegistered<HostingScan.HostingScanPacket>());
        Assert.True(resolved.IsRegistered<HostingScan.HostingScanAttributedPacket>());
        Assert.False(resolved.IsRegistered<HostingScan.Child.HostingScanChildPacket>());
    }

    [Fact]
    public void AddPacketNamespaceWhenAssemblyPathProvidedScopesNamespaceToThatAssembly()
    {
        NetworkApplicationBuilder builder = NetworkApplication.CreateBuilder();
        _ = builder.AddPacketNamespace(
            assemblyPath: typeof(HostingScan.HostingScanPacket).Assembly.Location,
            packetNamespace: RootNamespace,
            recursive: true);

        IPacketRegistry resolved = CreateRegistry(builder);

        Assert.True(resolved.IsRegistered<HostingScan.HostingScanPacket>());
        Assert.True(resolved.IsRegistered<HostingScan.HostingScanAttributedPacket>());
        Assert.True(resolved.IsRegistered<HostingScan.Child.HostingScanChildPacket>());
    }

    private static IPacketRegistry CreateRegistry(NetworkApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        FieldInfo stateField = typeof(NetworkApplicationBuilder).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Could not resolve builder state field.");

        object state = stateField.GetValue(builder)
            ?? throw new InvalidOperationException("Builder state is null.");

        MethodInfo createMethod = typeof(NetworkApplicationBuilder).GetMethod("CreatePacketRegistry", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Could not resolve packet registry creation method.");

        return (IPacketRegistry?)createMethod.Invoke(obj: null, parameters: [state])
            ?? throw new InvalidOperationException("Packet registry creation returned null.");
    }
}

namespace Nalix.Network.Test.HostingScan
{
public sealed class HostingScanPacket : PacketBase<HostingScanPacket>
{
    [SerializeOrder(PacketHeaderOffset.Region)]
    public ushort Value { get; set; }

    public static new HostingScanPacket Deserialize(ReadOnlySpan<byte> buffer)
        => PacketBase<HostingScanPacket>.Deserialize(buffer);
}

[Packet]
public sealed class HostingScanAttributedPacket : PacketBase<HostingScanAttributedPacket>
{
    [SerializeOrder(PacketHeaderOffset.Region)]
    public ushort Value { get; set; }

    public static new HostingScanAttributedPacket Deserialize(ReadOnlySpan<byte> buffer)
        => PacketBase<HostingScanAttributedPacket>.Deserialize(buffer);
}
}

namespace Nalix.Network.Test.HostingScan.Child
{
public sealed class HostingScanChildPacket : PacketBase<HostingScanChildPacket>
{
    [SerializeOrder(PacketHeaderOffset.Region)]
    public ushort Value { get; set; }

    public static new HostingScanChildPacket Deserialize(ReadOnlySpan<byte> buffer)
        => PacketBase<HostingScanChildPacket>.Deserialize(buffer);
}
}
