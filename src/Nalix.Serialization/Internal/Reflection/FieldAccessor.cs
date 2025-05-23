using System;
using System.Reflection;

namespace Nalix.Serialization.Internal.Reflection;

internal sealed partial class FieldAccessor<T>
{
    public string Name { get; init; }
    public Type FieldType { get; init; }

    public Func<T, object> Getter { get; init; }
    public Action<T, object> Setter { get; init; }

    public int Order { get; init; }
    public bool IsIgnored { get; init; }

    public static FieldAccessor<T> Create(MemberInfo memberInfo, int order = 0, bool isIgnored = false)
    {
        if (memberInfo is PropertyInfo property)
        {
            return new FieldAccessor<T>
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
            return new FieldAccessor<T>
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
