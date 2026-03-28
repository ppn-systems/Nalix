// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Network.Examples.Attributes;

/// <summary>
/// Marks a packet handler with a human-readable tag for the example metadata pipeline.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class PacketTagAttribute(string tag) : Attribute
{
    /// <summary>
    /// Gets the tag value attached to the handler.
    /// </summary>
    public string Tag { get; } = tag;
}
