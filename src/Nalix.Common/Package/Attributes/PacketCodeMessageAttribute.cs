namespace Nalix.Common.Package.Attributes;

/// <summary>
/// Specifies a human-readable error message associated with a specific <see cref="System.Enum"/> value.
/// Typically used with <see cref="Enums.PacketCode"/> to provide error descriptions for clients.
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Field, AllowMultiple = false)]
public sealed class PacketCodeMessageAttribute(string message) : System.Attribute
{
    /// <summary>
    /// Gets the descriptive error message associated with the enum value.
    /// </summary>
    public string Message { get; } = message;
}
