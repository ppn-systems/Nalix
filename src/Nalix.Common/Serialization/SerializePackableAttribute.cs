namespace Nalix.Common.Serialization;

/// <summary>
/// An attribute that marks a class, struct, or interface as serializable with a specified layout.
/// This attribute is used to configure the serialization behavior for types in the Nalix serialization framework.
/// </summary>
[System.AttributeUsage(
    System.AttributeTargets.Class |
    System.AttributeTargets.Struct |
    System.AttributeTargets.Interface,
    AllowMultiple = false, Inherited = false)]
public sealed class SerializePackableAttribute : System.Attribute
{
    /// <summary>
    /// Gets the layout strategy to be used during serialization of the marked type.
    /// The default value is <see cref="SerializeLayout.Sequential"/> if not explicitly set.
    /// </summary>
    public SerializeLayout SerializeLayout { get; }
}
