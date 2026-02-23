// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Reflection;

namespace Nalix.Runtime.Dispatching;

/// <summary>
/// Contributes metadata for packet handler registration.
/// Providers can inspect the handler method and populate a builder with
/// attributes that later become part of the final packet metadata.
/// </summary>
public interface IPacketMetadataProvider
{
    /// <summary>
    /// Populates the <see cref="PacketMetadataBuilder"/> with metadata for the
    /// specified handler method.
    /// </summary>
    /// <param name="method">The handler method being inspected.</param>
    /// <param name="builder">
    /// The metadata builder to populate with attributes and metadata.
    /// </param>
    void Populate(MethodInfo method, PacketMetadataBuilder builder);
}
