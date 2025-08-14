// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Packets.Attributes;

/// <summary>
/// Specifies that the target class is a packet controller responsible for handling packet commands.
/// </summary>
/// <remarks>
/// This attribute can optionally specify a controller name, active status, and version number
/// for logging, debugging, or feature management purposes.
/// </remarks>
/// <param name="name">
/// The human-readable name of the packet controller.
/// Defaults to <c>"Unknown"</c> if not provided.
/// </param>
/// <param name="isActive">
/// Indicates whether the controller is active and should handle packets.
/// Defaults to <c>true</c>.
/// </param>
/// <param name="version">
/// The version identifier for the packet controller.
/// Defaults to <c>"1.0"</c>.
/// </param>
[System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class PacketControllerAttribute(
    System.String name = "Unknown",
    System.Boolean isActive = true,
    System.String version = "1.0") : System.Attribute
{
    /// <summary>
    /// Gets the name of the packet controller.
    /// Used primarily for logging and debugging purposes.
    /// </summary>
    public System.String Name { get; } = name;

    /// <summary>
    /// Gets the version string of the packet controller.
    /// </summary>
    public System.String Version { get; } = version;

    /// <summary>
    /// Gets a value indicating whether the packet controller is active.
    /// </summary>
    public System.Boolean IsActive { get; } = isActive;
}
