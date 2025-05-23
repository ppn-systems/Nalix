using Nalix.Common.Serialization;

namespace Nalix.Serialization.Internal.Types;

internal static partial class TypeMetadata
{
    private static class Cache<[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(PropertyAccess)] T>
    {
        public static bool IsReferenceOrNullable;
        public static bool IsUnmanagedSZArray;
        public static bool IsFixedSizeSerializable = false;

        public static int UnmanagedSZArrayElementSize;
        public static int SerializableFixedSize = 0;

        static Cache()
        {
            try
            {
                System.Type type = typeof(T);
                IsReferenceOrNullable = !type.IsValueType || System.Nullable.GetUnderlyingType(type) != null;

                if (type.IsSZArray)
                {
                    System.Type elementType = type.GetElementType();
                    if (elementType != null && !IsReferenceOrContainsReferences(elementType))
                    {
                        IsUnmanagedSZArray = true;
                        UnmanagedSZArrayElementSize = UnsafeSizeOf(elementType);
                    }
                }
                else
                {
                    if (typeof(IFixedSizeSerializable).IsAssignableFrom(type))
                    {
                        System.Reflection.PropertyInfo prop = type.GetProperty(
                            nameof(IFixedSizeSerializable.Size),
                            System.Reflection.BindingFlags.Public |
                            System.Reflection.BindingFlags.Static |
                            System.Reflection.BindingFlags.NonPublic |
                            System.Reflection.BindingFlags.FlattenHierarchy
                        );

                        if (prop != null)
                        {
                            IsFixedSizeSerializable = true;
                            SerializableFixedSize = (int)prop.GetValue(null)!;
                        }
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
