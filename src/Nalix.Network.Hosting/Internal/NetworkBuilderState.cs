// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Network.Routing;

namespace Nalix.Network.Hosting.Internal;

internal sealed class NetworkBuilderState
{
    public List<OptionRegistration> OptionRegistrations { get; } = [];

    public List<PacketAssemblyRegistration> PacketAssemblies { get; } = [];

    public HashSet<Assembly> HandlerAssemblies { get; } = [];

    public List<HandlerRegistration> HandlerRegistrations { get; } = [];

    public List<MetadataProviderRegistration> MetadataProviderRegistrations { get; } = [];

    public List<TcpServerRegistration> TcpServerRegistrations { get; } = [];

    public List<UdpServerRegistration> UdpServerRegistrations { get; } = [];

    public List<Action<PacketDispatchOptions<IPacket>>> PacketDispatchConfigurations { get; } = [];

    public ILogger Logger { get; set; } = NullLogger.Instance;
}

internal sealed record OptionRegistration(Type OptionsType, Action<object> Apply);

internal sealed record PacketAssemblyRegistration(Assembly Assembly, bool RequirePacketAttribute);

internal sealed record HandlerRegistration(Type HandlerType, Func<object> Factory);

internal sealed record MetadataProviderRegistration(Type ProviderType, Func<IPacketMetadataProvider> Factory);

internal sealed record TcpServerRegistration(Type ProtocolType, Func<IPacketDispatch, IProtocol> Factory);

internal sealed record UdpServerRegistration(Type ProtocolType, Func<IPacketDispatch, IProtocol> Factory);
