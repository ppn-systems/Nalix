using Nalix.Common.Serialization;
using Nalix.Serialization.Internal.Reflection;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Nalix.Serialization.Internal.Types;

internal static partial class TypeMetadata
{
    /// <summary>
    /// Tính thông tin cho array types.
    /// </summary>
    private static (bool IsUnmanagedArray, int ElementSize) CalculateArrayInfo(Type type)
    {
        var elementType = type.GetElementType();
        if (elementType is null || IsReferenceOrContainsReferences(elementType))
        {
            return (false, 0);
        }

        var elementSize = UnsafeSizeOf(elementType);
        return (elementSize > 0, elementSize);
    }

    /// <summary>
    /// Tính thông tin cho IFixedSizeSerializable types.
    /// </summary>
    private static (bool IsFixed, int Size) CalculateFixedSizeSerializableInfo(
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(PropertyAccess)] Type type)
    {
        try
        {
            var prop = type.GetProperty(
                nameof(IFixedSizeSerializable.Size),
                BindingFlags.Static | Flags
            );

            if (prop is not null)
            {
                var size = (int)prop.GetValue(null)!;
                return (true, size);
            }
        }
        catch
        {
            // Fall through
        }

        return (false, 0);
    }

    /// <summary>
    /// Tính thông tin cho composite types (class/struct thường).
    /// </summary>
    private static (bool IsFixed, bool IsComposite, int FixedSize, int CompositeSize) CalculateCompositeTypeInfo<T>()
    {
        try
        {
            var fields = FieldCache<T>.GetFields();

            if (fields.Length is 0)
            {
                return (true, false, 0, 0); // Empty type = fixed size 0
            }

            var totalSize = CalculateTotalFieldsSize(fields);

            if (totalSize > 0)
            {
                // Tất cả fields có fixed size
                return (true, false, totalSize, 0);
            }
            else if (totalSize is 0)
            {
                // Có dynamic fields
                var estimatedSize = CalculateEstimatedSize(fields);
                return (false, true, 0, estimatedSize);
            }

            // Error case
            return (false, false, 0, 0);
        }
        catch
        {
            return (false, false, 0, 0);
        }
    }

    /// <summary>
    /// Tính tổng size từ fields. Returns > 0 nếu tất cả fixed, 0 nếu có dynamic, nhỏ 0 nếu error.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CalculateTotalFieldsSize(ReadOnlySpan<FieldSchema> fields)
    {
        var totalSize = 0;
        var hasFixedFields = false;
        var hasDynamicFields = false;

        for (var i = 0; i < fields.Length; i++)
        {
            var field = fields[i];
            var fieldSize = CalculateFieldSize(field.FieldType, field.FieldInfo);

            if (fieldSize > 0)
            {
                totalSize += fieldSize;
                hasFixedFields = true;
            }
            else if (fieldSize is 0)
            {
                hasDynamicFields = true;
            }
            else
            {
                return -1; // Error
            }
        }

        return hasDynamicFields ? 0 : (hasFixedFields ? totalSize : 0);
    }

    /// <summary>
    /// Tính estimated size cho composite types có dynamic fields.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CalculateEstimatedSize(ReadOnlySpan<FieldSchema> fields)
    {
        var estimatedSize = 0;

        for (var i = 0; i < fields.Length; i++)
        {
            var field = fields[i];
            var fieldSize = CalculateFieldSize(field.FieldType, field.FieldInfo);

            if (fieldSize > 0)
            {
                estimatedSize += fieldSize;
            }
            else
            {
                estimatedSize += GetEstimatedFieldSize(field.FieldType, field.FieldInfo);
            }
        }

        return estimatedSize;
    }

    /// <summary>
    /// Tính size cho một field cụ thể.
    /// Returns > 0 cho fixed size, 0 cho dynamic size, -1 cho unknown/error.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CalculateFieldSize(
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(PropertyAccess)] Type fieldType, FieldInfo fieldInfo)
    {
        // Dynamic types
        if (IsDynamicType(fieldType))
        {
            return 0;
        }

        // Primitive types
        var primitiveSize = GetPrimitiveTypeSize(fieldType);
        if (primitiveSize > 0)
            return primitiveSize;

        // Nullable types
        var underlyingType = Nullable.GetUnderlyingType(fieldType);
        if (underlyingType is not null)
        {
            var underlyingSize = CalculateFieldSize(underlyingType, fieldInfo);
            return underlyingSize > 0 ? underlyingSize + 1 : underlyingSize;
        }

        // Unmanaged value types
        if (fieldType.IsValueType && !IsReferenceOrContainsReferences(fieldType))
        {
            return UnsafeSizeOf(fieldType);
        }

        // IFixedSizeSerializable
        if (typeof(IFixedSizeSerializable).IsAssignableFrom(fieldType))
        {
            var (isFixed, size) = CalculateFixedSizeSerializableInfo(fieldType);
            return isFixed ? size : -1;
        }

        return -1; // Unknown
    }

    /// <summary>
    /// Lấy estimated size cho dynamic field types.
    /// </summary>
    private static int GetEstimatedFieldSize(
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(PropertyAccess)] Type fieldType, FieldInfo fieldInfo)
    {
        // Kiểm tra SerializeDynamicSizeAttribute
        SerializeDynamicSizeAttribute dynamicAttr = fieldInfo.GetCustomAttribute<SerializeDynamicSizeAttribute>()
            ?? GetPropertyDynamicAttribute(fieldInfo);

        if (dynamicAttr is not null)
            return dynamicAttr.Size;

        // Default estimates
        return fieldType switch
        {
            _ when fieldType == typeof(string) => 64,
            _ when fieldType == typeof(byte[]) => 256,
            _ when fieldType == typeof(char[]) => 128,
            _ when fieldType.IsArray => GetArrayEstimate(fieldType),
            _ when fieldType.IsClass => 32,
            _ => 16
        };
    }

    /// <summary>
    /// Kiểm tra xem type có phải dynamic size không.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsDynamicType(Type type)
    {
        if (type == typeof(string))
            return true;

        if (type.IsArray)
            return true;

        if (type.IsGenericType)
        {
            var genericDef = type.GetGenericTypeDefinition();
            return genericDef == typeof(List<>) ||
                   genericDef == typeof(IList<>) ||
                   genericDef == typeof(ICollection<>) ||
                   genericDef == typeof(IEnumerable<>);
        }

        if (type.IsClass && !typeof(IFixedSizeSerializable).IsAssignableFrom(type))
            return true;

        return false;
    }

    /// <summary>
    /// Lấy size cho primitive types.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetPrimitiveTypeSize(Type type)
    {
        if (type == typeof(bool) || type == typeof(byte) || type == typeof(sbyte))
            return 1;
        if (type == typeof(short) || type == typeof(ushort) || type == typeof(char))
            return 2;
        if (type == typeof(int) || type == typeof(uint) || type == typeof(float))
            return 4;
        if (type == typeof(long) || type == typeof(ulong) || type == typeof(double))
            return 8;
        if (type == typeof(decimal) || type == typeof(Guid))
            return 16;
        if (type == typeof(DateTime))
            return 8;
        if (type == typeof(nint) || type == typeof(nuint))
            return IntPtr.Size;

        return 0;
    }

    /// <summary>
    /// Tìm SerializeDynamicSizeAttribute trên property tương ứng.
    /// </summary>
    private static SerializeDynamicSizeAttribute GetPropertyDynamicAttribute(FieldInfo field)
    {
        var declaringType = field.DeclaringType;
        if (declaringType is null) return null;

        if (field.Name.StartsWith('<') && field.Name.Contains(">k__BackingField"))
        {
            var propertyName = field.Name[1..field.Name.IndexOf('>')];
            var property = declaringType.GetProperty(propertyName, Flags);
            return property?.GetCustomAttribute<SerializeDynamicSizeAttribute>();
        }

        return null;
    }

    /// <summary>
    /// Estimate size cho array types.
    /// </summary>
    private static int GetArrayEstimate(Type arrayType)
    {
        var elementType = arrayType.GetElementType();
        if (elementType is null) return 64;

        if (elementType.IsValueType)
        {
            var elementSize = GetPrimitiveTypeSize(elementType);
            if (elementSize > 0)
                return 10 * elementSize;

            var unsafeSize = UnsafeSizeOf(elementType);
            return unsafeSize > 0 ? 10 * unsafeSize : 64;
        }

        return 10 * 8; // Reference types
    }
}
