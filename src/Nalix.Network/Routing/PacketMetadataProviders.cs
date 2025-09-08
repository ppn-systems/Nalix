// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Network.Routing;

/// <summary>
/// Provides a global registry of <see cref="IPacketMetadataProvider"/> instances
/// that participate in building <c>PacketMetadata</c> for handler methods.
/// </summary>
public static class PacketMetadataProviders
{
    private static readonly System.Collections.Generic.List<IPacketMetadataProvider> _providers = [];

    /// <summary>
    /// Registers a new <see cref="IPacketMetadataProvider"/> instance.
    /// </summary>
    /// <param name="provider">The provider to register.</param>
    public static void Register(IPacketMetadataProvider provider) => _providers.Add(provider);

    /// <summary>
    /// Gets the registered metadata providers.
    /// </summary>
    internal static System.Collections.Generic.IReadOnlyList<IPacketMetadataProvider> Providers => _providers;
}