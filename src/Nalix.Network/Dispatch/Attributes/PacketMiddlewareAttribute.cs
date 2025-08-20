// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Network.Dispatch.Enums;

namespace Nalix.Network.Dispatch.Attributes;

/// <summary>
/// Attribute to mark a class as a packet middleware and specify its stage, order, and optional name.
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Class, Inherited = false)]
public sealed class PacketMiddlewareAttribute(
    MiddlewareStage stage,
    System.Int32 order = 0,
    System.String? name = null) : System.Attribute
{
    /// <summary>
    /// Gets the optional name of the middleware.
    /// </summary>
    public System.String? Name { get; } = name;

    /// <summary>
    /// Gets the order in which the middleware is executed.
    /// </summary>
    public System.Int32 Order { get; } = order;

    /// <summary>
    /// Gets the middleware stage.
    /// </summary>
    public MiddlewareStage Stage { get; } = stage;
}
