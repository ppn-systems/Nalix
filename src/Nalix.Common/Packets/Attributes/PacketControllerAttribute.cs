namespace Nalix.Common.Packets.Attributes;

/// <summary>
/// Marks a class as a packet controller responsible for handling packet commands.
/// Optionally, a name, active status, and version can be provided for logging and debugging purposes.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="PacketControllerAttribute"/> class.
/// </remarks>
/// <param name="name">The name of the packet controller. Defaults to "Unknown" if not provided.</param>
/// <param name="isActive">Indicates whether the controller is active. Defaults to true.</param>
/// <param name="version">The version of the packet controller. Defaults to "1.0".</param>
[System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class PacketControllerAttribute(
    System.String name = "Unknown",
    System.Boolean isActive = true,
    System.String version = "1.0") : System.Attribute
{
    /// <summary>
    /// The name of the packet controller, used for logging and debugging.
    /// If no name is provided, defaults to "Unknown".
    /// </summary>
    public System.String Name { get; } = name;

    /// <summary>
    /// The version of the packet controller. Default is "1.0".
    /// </summary>
    public System.String Version { get; } = version;

    /// <summary>
    /// The active status of the packet controller. Default is true.
    /// </summary>
    public System.Boolean IsActive { get; } = isActive;
}
