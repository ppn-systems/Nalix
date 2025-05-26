using Nalix.Common.Serialization;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Nalix.Serialization.Internal.Reflection;

internal static partial class HybridFieldAccess<[
    DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields)] T>
{
    // Cache cho các loại field khác nhau
    private static readonly FieldAccessor[] _fieldAccessors;

    private static readonly FieldInfo[] _fields;

    // Enum để phân loại cách access
    private enum AccessType : byte
    {
        UnsafeValueType,    // int, float, bool... -> unsafe direct
        ReferenceType,      // string, object... -> reflection optimized
        NullableValueType,  // int?, DateTime?... -> special handling
        ComplexType         // custom class/struct -> recursive
    }

    // Struct chứa thông tin access cho mỗi field
    private readonly struct FieldAccessor(
        AccessType type,
        int offset,
        FieldInfo fieldInfo,
        Func<T, object> getter,
        Action<T, object> setter,
        Type fieldType)
    {
        public readonly int Offset = offset;           // Cho unsafe access
        public readonly Type FieldType = fieldType;
        public readonly AccessType Type = type;
        public readonly FieldInfo FieldInfo = fieldInfo; // Cho reflection access
        public readonly Func<T, object> Getter = getter;
        public readonly Action<T, object> Setter = setter;
    }

    static HybridFieldAccess()
    {
        (_fields, _fieldAccessors) = BuildAccessors();

        Console.WriteLine($"=== Hybrid Access for {typeof(T).Name} ===");
        for (int i = 0; i < _fieldAccessors.Length; i++)
        {
            var accessor = _fieldAccessors[i];
            Console.WriteLine($"Field {_fields[i].Name}: {accessor.Type} access");
        }
    }

    private static (FieldInfo[] fields, FieldAccessor[] accessors) BuildAccessors()
    {
        var type = typeof(T);
        var allFields = type.GetFields(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        var validFields = new List<FieldInfo>();
        var accessors = new List<FieldAccessor>();

        foreach (var field in allFields)
        {
            // Bỏ qua backing fields và ignored fields
            if (ShouldIgnoreField(field)) continue;

            var accessor = CreateAccessorForField(field);

            validFields.Add(field);
            accessors.Add(accessor);
        }

        return (validFields.ToArray(), accessors.ToArray());
    }

    private static FieldAccessor CreateAccessorForField(FieldInfo field)
    {
        var fieldType = field.FieldType;
        var accessType = DetermineAccessType(fieldType);

        return accessType switch
        {
            AccessType.UnsafeValueType => CreateUnsafeAccessor(field),
            AccessType.ReferenceType => CreateReferenceAccessor(field),
            AccessType.NullableValueType => CreateNullableAccessor(field),
            AccessType.ComplexType => CreateComplexAccessor(field),
            _ => throw new NotSupportedException($"Unsupported field type: {fieldType}")
        };
    }

    private static AccessType DetermineAccessType(Type fieldType)
    {
        // Value types không có reference -> unsafe
        if (fieldType.IsValueType && !fieldType.IsGenericType)
        {
            return AccessType.UnsafeValueType;
        }

        // Nullable value types
        if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            return AccessType.NullableValueType;
        }

        // String và các reference types built-in
        if (fieldType == typeof(string) || !fieldType.IsValueType)
        {
            return AccessType.ReferenceType;
        }

        return AccessType.ComplexType;
    }

    // Unsafe accessor cho value types
    private static FieldAccessor CreateUnsafeAccessor(FieldInfo field)
    {
        var offset = 0; // Tính offset nếu cần unsafe

        // Tạo compiled getter/setter cho performance
        var getter = CreateCompiledGetter(field);
        var setter = CreateCompiledSetter(field);

        return new FieldAccessor(AccessType.UnsafeValueType, offset, field, getter, setter, field.FieldType);
    }

    // Reference accessor cho string, object...
    private static FieldAccessor CreateReferenceAccessor(FieldInfo field)
    {
        // Optimized reflection với caching
        var getter = CreateCompiledGetter(field);
        var setter = CreateCompiledSetter(field);

        return new FieldAccessor(AccessType.ReferenceType, -1, field, getter, setter, field.FieldType);
    }

    // Nullable accessor
    private static FieldAccessor CreateNullableAccessor(FieldInfo field)
    {
        var getter = CreateCompiledGetter(field);
        var setter = CreateCompiledSetter(field);

        return new FieldAccessor(AccessType.NullableValueType, -1, field, getter, setter, field.FieldType);
    }

    // Complex type accessor (recursive)
    private static FieldAccessor CreateComplexAccessor(FieldInfo field)
    {
        var getter = CreateCompiledGetter(field);
        var setter = CreateCompiledSetter(field);

        return new FieldAccessor(AccessType.ComplexType, -1, field, getter, setter, field.FieldType);
    }

    // Compiled expression trees cho performance cao
    private static Func<T, object> CreateCompiledGetter(FieldInfo field)
    {
        // Sử dụng Expression.Compile() hoặc delegates
        return (obj) => field.GetValue(obj);
    }

    private static Action<T, object> CreateCompiledSetter(FieldInfo field)
    {
        return (obj, value) => field.SetValue(obj, value);
    }

    // Public API
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object GetFieldValue(T obj, int fieldIndex)
    {
        return _fieldAccessors[fieldIndex].Getter(obj);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetFieldValue(T obj, int fieldIndex, object value)
    {
        _fieldAccessors[fieldIndex].Setter(obj, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<FieldInfo> GetFields() => _fields.AsSpan();

    // Specialized methods cho các type phổ biến
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetStringField(T obj, int fieldIndex)
    {
        var accessor = _fieldAccessors[fieldIndex];
        return accessor.Type == AccessType.ReferenceType
            ? (string)accessor.Getter(obj)
            : throw new InvalidOperationException("Field is not a string");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetInt32Field(T obj, int fieldIndex)
    {
        var accessor = _fieldAccessors[fieldIndex];
        return accessor.Type == AccessType.UnsafeValueType
            ? (int)accessor.Getter(obj)
            : throw new InvalidOperationException("Field is not an int32");
    }

    private static bool ShouldIgnoreField(FieldInfo field)
    {
        return (field.Name.StartsWith('<') && field.Name.Contains(">k__BackingField")) ||
               field.GetCustomAttribute<SerializeIgnoreAttribute>() is not null;
    }
}
