// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Network.Routing;
using System.Reflection;

namespace Nalix.Network.Examples.Custom;

/// <summary>
/// A metadata provider that reads <see cref="PacketCustomAttribute"/> from handler methods
/// and adds it to the <see cref="PacketMetadataBuilder"/> custom attributes.
/// </summary>
public sealed class PacketCustomAttributeProvider : IPacketMetadataProvider
{
    /// <summary>
    /// Populate the builder with the PacketCustomAttribute, if present.
    /// </summary>
    /// <param name="method">The handler method being inspected.</param>
    /// <param name="builder">The metadata builder to populate.</param>
    public void Populate(MethodInfo method, PacketMetadataBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(builder);

        // Try to get the custom attribute from the method
        var attr = method.GetCustomAttribute<PacketCustomAttribute>(inherit: false);
        if (attr != null)
        {
            // Store the attribute in the builder's custom attribute bag
            builder.Add(attr);
        }
    }
}
