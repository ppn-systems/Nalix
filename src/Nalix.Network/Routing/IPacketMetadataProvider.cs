// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Reflection;

namespace Nalix.Network.Routing;

/// <summary>
/// Defines a contract for components that can contribute metadata
/// for packet handler methods.
/// </summary>
public interface IPacketMetadataProvider
{
    /// <summary>
    /// Populates the <see cref="PacketMetadataBuilder"/> with metadata
    /// for the specified handler method.
    /// </summary>
    /// <param name="method">The handler method being inspected.</param>
    /// <param name="builder">
    /// The metadata builder to populate with attributes and metadata.
    /// </param>
    void Populate(MethodInfo method, PacketMetadataBuilder builder);
}