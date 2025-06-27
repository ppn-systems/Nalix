namespace Nalix.Shared.Configuration.Attributes;

/// <summary>
/// An attribute that indicates that a property should be ignored during configuration container initialization.
/// </summary>
/// <remarks>
/// Properties marked with this attribute will not be set when loading values from a configuration file.
/// You can optionally provide a reason for why the property is ignored.
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="ConfiguredIgnoreAttribute"/> class.
/// </remarks>
/// <param name="reason">The optional reason for ignoring the property.</param>
[System.AttributeUsage(System.AttributeTargets.Property)]
public class ConfiguredIgnoreAttribute(System.String? reason = null) : System.Attribute
{
    /// <summary>
    /// Optional reason for ignoring the property during configuration.
    /// </summary>
    public System.String Reason { get; } = reason ?? System.String.Empty;
}
