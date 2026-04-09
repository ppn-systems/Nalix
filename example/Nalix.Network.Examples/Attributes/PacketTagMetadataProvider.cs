// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Reflection;
using Nalix.Runtime.Dispatching;

namespace Nalix.Network.Examples.Attributes;

/// <summary>
/// Copies <see cref="PacketTagAttribute"/> from a handler method into packet metadata.
/// </summary>
public sealed class PacketTagMetadataProvider : IPacketMetadataProvider
{
    /// <summary>
    /// Adds the custom tag attribute to the metadata builder when it is present.
    /// </summary>
    public void Populate(MethodInfo method, PacketMetadataBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(builder);

        PacketTagAttribute? attribute = method.GetCustomAttribute<PacketTagAttribute>(inherit: false);
        if (attribute is not null)
        {
            builder.Add(attribute);
        }
    }
}
