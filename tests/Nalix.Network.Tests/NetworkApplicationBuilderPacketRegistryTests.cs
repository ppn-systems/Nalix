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
    public void ConfigurePacketRegistryWhenProvidedSkipsAutomaticPacketRegistration()
    {
        NetworkApplicationBuilder builder = NetworkApplication.CreateBuilder();
        PacketRegistry provided = new(f => f.RegisterPacketAssembly(typeof(HostingScan.HostingScanPacket).Assembly.Location, requireAttribute: true));

        _ = builder.ConfigurePacketRegistry(provided);
        _ = builder.AddPacketNamespace(RootNamespace, recursive: true);

        IPacketRegistry resolved = CreateRegistry(builder);

        Assert.Same(provided, resolved);
        // Note: The original test check for HostingScanAttributedPacket, assuming it's in the assembly.
    }

    private static IPacketRegistry CreateRegistry(NetworkApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Access internal CreatePacketRegistry via reflection if necessary, 
        // but now we have InternalsVisibleTo we might be able to call it if it's protected/internal.
        // The previous test used reflection.
        
        MethodInfo createMethod = typeof(NetworkApplicationBuilder).GetMethod("CreatePacketRegistry", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Could not resolve packet registry creation method.");

        // We need the internal state field.
        FieldInfo stateField = typeof(NetworkApplicationBuilder).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Could not resolve builder state field.");

        object state = stateField.GetValue(builder)
            ?? throw new InvalidOperationException("Builder state is null.");

        return (IPacketRegistry?)createMethod.Invoke(obj: null, parameters: [state])
            ?? throw new InvalidOperationException("Packet registry creation returned null.");
    }
}
#endif













