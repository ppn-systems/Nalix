// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

namespace Nalix.Runtime.Dispatching;

/// <summary>
/// Holds the global packet metadata providers that contribute attributes during
/// handler registration.
/// Providers are evaluated in registration order.
/// </summary>
public static class PacketMetadataProviders
{
    private static readonly List<IPacketMetadataProvider> s_providers = [];

    /// <summary>
    /// Gets the registered metadata providers in registration order.
    /// </summary>
    internal static IReadOnlyList<IPacketMetadataProvider> Providers => s_providers;

    /// <summary>
    /// Registers a new <see cref="IPacketMetadataProvider"/> instance.
    /// </summary>
    /// <param name="provider">The provider to register.</param>
    /// <remarks>
    /// Providers are appended, so later registrations can extend or override metadata
    /// behavior in a predictable order.
    /// </remarks>
    public static void Register(IPacketMetadataProvider provider) => s_providers.Add(provider);
}
