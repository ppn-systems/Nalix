using Nalix.Common.Security.Types;
using System;

namespace Nalix.Common.Packets.Attributes;

/// <summary>
/// Specifies a security guard to be applied to a method, enforcing specific security constraints.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="PacketGuardAttribute"/> class with the specified guard type.
/// </remarks>
/// <param name="type">The type of security guard to enforce.</param>
[System.Obsolete("This feature is not ready for public use yet.")]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public sealed class PacketGuardAttribute(GuardType type) : System.Attribute
{
    /// <summary>
    /// Gets the type of security guard to be applied.
    /// </summary>
    public GuardType Type { get; } = type;
}