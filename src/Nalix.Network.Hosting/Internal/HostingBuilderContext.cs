// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nalix.Common.Abstractions;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Network.Routing;
using Nalix.Runtime.Dispatching;

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Benchmarks")]
#endif

namespace Nalix.Network.Hosting.Internal;

/// <summary>
/// Represents the mutable state accumulated while building a network hosting pipeline.
/// </summary>
/// <remarks>
/// This context is used internally during application startup to collect
/// configuration, protocol bindings, packet metadata providers, and handler
/// registrations before the hosting runtime is finalized.
/// </remarks>
internal sealed class HostingBuilderContext
{
    /// <summary>
    /// Gets the registered options configurations.
    /// </summary>
    public List<OptionsConfiguration> Options { get; } = [];

    /// <summary>
    /// Gets the packet assemblies used for packet discovery.
    /// </summary>
    public List<PacketAssemblyDescriptor> PacketAssemblies { get; } = [];

    /// <summary>
    /// Gets the packet assembly paths used for packet discovery.
    /// </summary>
    public List<PacketAssemblyPathDescriptor> PacketAssemblyPaths { get; } = [];

    /// <summary>
    /// Gets the namespace filters used for packet discovery.
    /// </summary>
    public List<PacketNamespaceDescriptor> PacketNamespaces { get; } = [];

    /// <summary>
    /// Gets the assemblies scanned for packet handlers.
    /// </summary>
    public HashSet<Assembly> HandlerAssemblies { get; } = [];

    /// <summary>
    /// Gets the registered packet handler descriptors.
    /// </summary>
    public List<HandlerDescriptor> Handlers { get; } = [];

    /// <summary>
    /// Gets the packet metadata providers used during routing and dispatch.
    /// </summary>
    public List<PacketMetadataProviderDescriptor> MetadataProviders { get; } = [];

    /// <summary>
    /// Gets the TCP protocol bindings configured for the host.
    /// </summary>
    public List<TcpProtocolBinding> TcpBindings { get; } = [];

    /// <summary>
    /// Gets the UDP protocol bindings configured for the host.
    /// </summary>
    public List<UdpProtocolBinding> UdpBindings { get; } = [];

    /// <summary>
    /// Gets the list of hosted background services.
    /// </summary>
    public List<Func<IActivatableAsync>> HostedServices { get; } = [];

    /// <summary>
    /// Gets the configuration delegates applied to
    /// <see cref="PacketDispatchOptions{TPacket}"/>.
    /// </summary>
    public List<Action<PacketDispatchOptions<IPacket>>> PacketDispatchConfigurators { get; } = [];

    /// <summary>
    /// Gets or sets the logger used during host construction.
    /// </summary>
    /// <value>
    /// Defaults to <see cref="NullLogger.Instance"/> when no logger is provided.
    /// </value>
    public ILogger Logger { get; set; } = NullLogger.Instance;

    /// <summary>
    /// Gets or sets the optional path to the server identity certificate.
    /// </summary>
    public string? IdentityCertificatePath { get; set; }

    /// <summary>
    /// Gets or sets a pre-built packet registry. When provided, hosting skips
    /// automatic packet discovery and registration.
    /// </summary>
    public IPacketRegistry? PacketRegistryOverride { get; set; }
}

/// <summary>
/// Describes an options configuration applied during host building.
/// </summary>
/// <param name="OptionsType">
/// The options type being configured.
/// </param>
/// <param name="Apply">
/// The delegate that applies configuration to the options instance.
/// </param>
internal sealed record OptionsConfiguration(Type OptionsType, Action<object> Apply);

/// <summary>
/// Describes an assembly used for packet type discovery.
/// </summary>
/// <param name="Assembly">
/// The assembly containing packet definitions.
/// </param>
/// <param name="RequirePacketAttribute">
/// Indicates whether discovered types must be annotated with a packet attribute
/// to be considered valid packets.
/// </param>
internal sealed record PacketAssemblyDescriptor(
    Assembly Assembly,
    bool RequirePacketAttribute);

/// <summary>
/// Describes an assembly path used for packet type discovery.
/// </summary>
/// <param name="AssemblyPath">
/// The path to an assembly containing packet definitions.
/// </param>
/// <param name="RequirePacketAttribute">
/// Indicates whether discovered types must be annotated with a packet attribute
/// to be considered valid packets.
/// </param>
internal sealed record PacketAssemblyPathDescriptor(
    string AssemblyPath,
    bool RequirePacketAttribute);

/// <summary>
/// Describes a packet namespace filter used during packet discovery.
/// </summary>
/// <param name="PacketNamespace">The namespace to match.</param>
/// <param name="Recursive">
/// Indicates whether sub-namespaces should be included.
/// </param>
/// <param name="AssemblyPath">
/// Optional assembly path scope. When null, currently loaded assemblies are used.
/// </param>
internal sealed record PacketNamespaceDescriptor(
    string PacketNamespace,
    bool Recursive,
    string? AssemblyPath = null);

/// <summary>
/// Describes a packet handler and its creation strategy.
/// </summary>
/// <param name="HandlerType">
/// The concrete handler type.
/// </param>
/// <param name="Factory">
/// A factory delegate used to create handler instances.
/// </param>
internal sealed record HandlerDescriptor(
    Type HandlerType,
    Func<object> Factory);

/// <summary>
/// Describes a packet metadata provider registration.
/// </summary>
/// <param name="ProviderType">
/// The metadata provider type.
/// </param>
/// <param name="Factory">
/// A factory delegate used to create the metadata provider instance.
/// </param>
internal sealed record PacketMetadataProviderDescriptor(
    Type ProviderType,
    Func<IPacketMetadataProvider> Factory);

/// <summary>
/// Represents a binding between a TCP transport and a protocol implementation.
/// </summary>
/// <param name="ProtocolType">
/// The protocol runtime type.
/// </param>
/// <param name="Factory">
/// A factory delegate that creates the protocol using an
/// <see cref="IPacketDispatch"/> instance.
/// </param>
/// <param name="Port">
/// Optional explicit port to listen on. If null, the default configured port is used.
/// </param>
internal sealed record TcpProtocolBinding(
    Type ProtocolType,
    Func<IPacketDispatch, IProtocol> Factory,
    ushort? Port = null);

/// <summary>
/// Represents a binding between a protocol type and its creation factory for UDP.
/// </summary>
/// <param name="ProtocolType">The type of the protocol.</param>
/// <param name="Factory">The factory used to create the protocol instance.</param>
/// <param name="Port">The optional port to listen on.</param>
/// <param name="Authentication">The optional authentication predicate used to validate incoming datagrams.</param>
internal sealed record UdpProtocolBinding(
    Type ProtocolType,
    Func<IPacketDispatch, IProtocol> Factory,
    ushort? Port = null,
    Func<IConnection, System.Net.EndPoint, ReadOnlySpan<byte>, bool>? Authentication = null);
