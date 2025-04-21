namespace Nalix.Common.Package.Attributes;

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
[System.AttributeUsage(System.AttributeTargets.Class)]
public sealed class PacketControllerAttribute(
    string name = "Unknown", bool isActive = true, string version = "1.0") : System.Attribute
{
    /// <summary>
    /// The name of the packet controller, used for logging and debugging.
    /// If no name is provided, defaults to "Unknown".
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// The active status of the packet controller. Default is true.
    /// </summary>
    public bool IsActive { get; } = isActive;

    /// <summary>
    /// The version of the packet controller. Default is "1.0".
    /// </summary>
    public string Version { get; } = version;
}
