// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

namespace Nalix.Network.Routing;

/// <summary>
/// Provides a global registry of <see cref="IPacketMetadataProvider"/> instances
/// that participate in building <c>PacketMetadata</c> for handler methods.
/// </summary>
public static class PacketMetadataProviders
{
    private static readonly List<IPacketMetadataProvider> s_providers = [];

    /// <summary>
    /// Gets the registered metadata providers.
    /// </summary>
    internal static IReadOnlyList<IPacketMetadataProvider> Providers => s_providers;

    /// <summary>
    /// Registers a new <see cref="IPacketMetadataProvider"/> instance.
    /// </summary>
    /// <param name="provider">The provider to register.</param>
    public static void Register(IPacketMetadataProvider provider) => s_providers.Add(provider);
}
