using System;
using System.Reflection;

namespace Nalix.Serialization.Internal.Reflection;

/// <summary>
/// Provides fast access to a field or property of type <typeparamref name="TField"/> on the type <typeparamref name="T"/>.
/// </summary>
internal sealed partial class FieldAccessor<T, TField>
{
    /// <summary>
    /// Order for serialization or processing purposes.
    /// </summary>
    public int Order { get; init; }

    /// <summary>
    /// The name of the field or property.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Indicates whether this member should be ignored (e.g., for serialization).
    /// </summary>
    public bool IsIgnored { get; init; }

    /// <summary>
    /// The type of the field or property.
    /// </summary>
    public Type FieldType { get; init; }

    /// <summary>
    /// Delegate to get the value from an instance of <typeparamref name="T"/>.
    /// </summary>
    public Func<T, TField> Getter { get; init; }

    /// <summary>
    /// Delegate to set the value on an instance of <typeparamref name="T"/>.
    /// </summary>
    public Action<T, TField> Setter { get; init; }

    /// <summary>
    /// Creates a <see cref="FieldAccessor{T, TField}"/> for the specified <see cref="MemberInfo"/>.
    /// </summary>
    /// <param name="memberInfo">The member info, either a property or a field.</param>
    /// <param name="order">The order for serialization.</param>
    /// <param name="isIgnored">Indicates if this member is ignored.</param>
    /// <returns>A configured instance of <see cref="FieldAccessor{T, TField}"/>.</returns>
    /// <exception cref="NotSupportedException">Thrown if memberInfo is neither a field nor a property.</exception>
    public static FieldAccessor<T, TField> Create(MemberInfo memberInfo, int order = 0, bool isIgnored = false)
    {
        if (memberInfo is PropertyInfo property)
        {
            return new FieldAccessor<T, TField>
            {
                Name = property.Name,
                FieldType = property.PropertyType,
                Getter = CreateGetter(property),
                Setter = CreateSetter(property),
                Order = order,
                IsIgnored = isIgnored
            };
        }
        else if (memberInfo is FieldInfo field)
        {
            return new FieldAccessor<T, TField>
            {
                Name = field.Name,
                FieldType = field.FieldType,
                Getter = CreateGetter(field),
                Setter = CreateSetter(field),
                Order = order,
                IsIgnored = isIgnored
            };
        }

        throw new NotSupportedException($"Unsupported member type: {memberInfo.MemberType}");
    }
}
