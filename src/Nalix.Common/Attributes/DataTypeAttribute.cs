
namespace Nalix.Common.Attributes;

/// <summary>
/// Specifies the data type associated with a field.
/// </summary>
/// <remarks>
/// This attribute is used to indicate the specific data type for a field, enabling metadata-driven processing or validation.
/// It can only be applied to fields.
/// </remarks>
[System.AttributeUsage(System.AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class DataTypeAttribute : System.Attribute
{
    /// <summary>
    /// Gets the data type associated with the field.
    /// </summary>
    public System.Type DataType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DataTypeAttribute"/> class with the specified data type.
    /// </summary>
    /// <param name="dataType">The <see cref="System.Type"/> to associate with the field.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="dataType"/> is null.</exception>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Style", "IDE0290:Use primary constructor", Justification = "<Pending>")]
    public DataTypeAttribute(System.Type dataType)
        => DataType = dataType ?? throw new System.ArgumentNullException(nameof(dataType));
}