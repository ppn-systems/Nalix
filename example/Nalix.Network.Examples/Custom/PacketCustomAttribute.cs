// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Network.Examples.Custom;

/// <summary>
/// A sample custom packet attribute that carries a string tag.
/// </summary>
/// <remarks>
/// Initializes a new instance of <see cref="PacketCustomAttribute"/>.
/// </remarks>
/// <param name="tag">Custom tag to attach to the handler.</param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class PacketCustomAttribute(String tag) : Attribute
{
    /// <summary>
    /// Gets the custom tag value.
    /// </summary>
    public String Tag { get; } = tag;
}