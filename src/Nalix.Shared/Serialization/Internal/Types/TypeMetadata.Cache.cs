using Nalix.Common.Serialization;

namespace Nalix.Shared.Serialization.Internal.Types;

internal static partial class TypeMetadata
{
    private const System.Reflection.BindingFlags Flags =
        System.Reflection.BindingFlags.Public |
        System.Reflection.BindingFlags.Instance |
        System.Reflection.BindingFlags.NonPublic |
        System.Reflection.BindingFlags.FlattenHierarchy;

    public const System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes PropertyAccess =
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicProperties;

    private static class Cache<[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(PropertyAccess)] T>
    {
        public static bool IsUnmanaged;
        public static bool IsNullable;
        public static bool IsReference;
        public static bool IsUnmanagedSZArray;
        public static bool IsFixedSizeSerializable = false;
        public static bool IsCompositeSerializable = false;

        public static int SerializableFixedSize = 0;
        public static int CompositeSerializableSize = 0;
        public static int UnmanagedSZArrayElementSize = 0;

        static Cache()
        {
            System.Type type = typeof(T);

            try
            {
                IsReference = !type.IsValueType;
                IsNullable = System.Nullable.GetUnderlyingType(type) != null;
                IsUnmanaged = !System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<T>();

                if (type.IsSZArray)
                {
                    System.Type? elementType = type.GetElementType();
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
                        System.Reflection.PropertyInfo? prop = type.GetProperty(
                            nameof(IFixedSizeSerializable.Size),
                            System.Reflection.BindingFlags.Static | Flags
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
                IsCompositeSerializable = false;
            }
        }
    }
}