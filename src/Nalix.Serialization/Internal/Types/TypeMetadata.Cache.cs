using Nalix.Common.Serialization;
using System.Runtime.CompilerServices;

namespace Nalix.Serialization.Internal.Types;

internal static partial class TypeMetadata
{
    private const System.Reflection.BindingFlags Flags =
        System.Reflection.BindingFlags.Public |
        System.Reflection.BindingFlags.Instance |
        System.Reflection.BindingFlags.NonPublic |
        System.Reflection.BindingFlags.FlattenHierarchy;

    private static class Cache<[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(PropertyAccess)] T>
    {
        public static bool IsUnmanaged;
        public static bool IsNullable;
        public static bool IsReference;
        public static bool IsUnmanagedSZArray;
        public static bool IsFixedSizeSerializable = false;
        public static bool IsCompositeSerializable = false;

        public static int UnmanagedSZArrayElementSize;
        public static int SerializableFixedSize = 0;
        public static int CompositeSerializableSize = 0;

        static Cache()
        {
            System.Type type = typeof(T);

            try
            {
                IsReference = !type.IsValueType;
                IsNullable = System.Nullable.GetUnderlyingType(type) != null;
                IsUnmanaged = !RuntimeHelpers.IsReferenceOrContainsReferences<T>();

                if (type.IsSZArray)
                {
                    System.Type elementType = type.GetElementType();
                    if (elementType != null && !IsReferenceOrContainsReferences(elementType))
                    {
                        IsUnmanagedSZArray = true;
                        UnmanagedSZArrayElementSize = UnsafeSizeOf(elementType);
                    }
                }
                else if (typeof(IFixedSizeSerializable).IsAssignableFrom(type))
                {
                    System.Reflection.PropertyInfo prop = type.GetProperty(
                        nameof(IFixedSizeSerializable.Size),
                        System.Reflection.BindingFlags.Static | Flags
                    );

                    if (prop != null)
                    {
                        IsFixedSizeSerializable = true;
                        SerializableFixedSize = (int)prop.GetValue(null)!;
                    }
                }
                else
                {
                    if (type.IsClass || type.IsValueType)
                    {
                    }
                }
            }
            catch
            {
                IsUnmanagedSZArray = false;
                IsFixedSizeSerializable = false;
            }
        }
    }
}
