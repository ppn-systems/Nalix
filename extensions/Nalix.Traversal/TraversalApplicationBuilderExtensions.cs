// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Hosting;
using Nalix.Network.Options;
using Nalix.Traversal.Handlers;
using Nalix.Traversal.Reflector;

namespace Nalix.Traversal;

/// <summary>
/// Provides extension methods for <see cref="INetworkApplicationBuilder"/> to configure the Traversal module.
/// </summary>
public static class TraversalApplicationBuilderExtensions
{
    /// <summary>
    /// Registers the NAT Traversal module into the Nalix networking pipeline.
    /// This enables peer-to-peer hole punching signaling capabilities.
    /// </summary>
    /// <param name="builder">The network application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static INetworkApplicationBuilder UseTraversal(this INetworkApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Register the Traversal handlers into the Dispatch Options
        _ = builder.ConfigureDispatchOptions(options =>
        {
            _ = options.WithHandler<PeerSignalHandler>();
            _ = options.WithHandler<ReflectorInitHandler>();
        });

        // Initialize Reflector manager
        ReflectorManager manager = new();
        Framework.Injection.InstanceManager.Instance.Register(manager);

        // We use NetworkSocketOptions to determine the port (Main Port + 1)
        NetworkSocketOptions netOptions = Environment.Configuration.ConfigurationManager.Instance.Get<Nalix.Network.Options.NetworkSocketOptions>();
        ushort ReflectorPort = (ushort)(netOptions.Port + 1);

        // Bind UdpPassthroughListener for Reflector using the native builder API
        _ = builder.BindUdp<Nalix.Traversal.Reflector.ReflectorProtocol>()
            .WithMode(Abstractions.Networking.OperatingMode.Passthrough)
            .WithFactory(_ => new Nalix.Traversal.Reflector.ReflectorProtocol(manager))
            .OnPort(ReflectorPort)
            .Bind();

        return builder;
    }
}
