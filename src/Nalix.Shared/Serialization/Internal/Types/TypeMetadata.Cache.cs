// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Serialization;


#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Serialization.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Tests.Serialization")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Benchmarks.Serialization")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Serialization.Benchmarks")]
#endif

namespace Nalix.Shared.Serialization.Internal.Types;

internal static partial class TypeMetadata
{
    private const System.Reflection.BindingFlags Flags =
        System.Reflection.BindingFlags.Static |
        System.Reflection.BindingFlags.Public |
        System.Reflection.BindingFlags.Instance |
        System.Reflection.BindingFlags.NonPublic |
        System.Reflection.BindingFlags.FlattenHierarchy;

    public const System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes PropertyAccess =
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicProperties;

    private static class Cache<[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(PropertyAccess)] T>
    {
        public static System.Boolean IsUnmanaged;
        public static System.Boolean IsNullable;
        public static System.Boolean IsReference;
        public static System.Boolean IsUnmanagedSZArray;
        public static System.Boolean IsFixedSizeSerializable = false;
        public static System.Boolean IsCompositeSerializable = false;

        public static System.Int32 SerializableFixedSize = 0;
        public static System.Int32 UnmanagedSZArrayElementSize = 0;

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
                        System.Reflection.PropertyInfo? prop = type.GetProperty(nameof(IFixedSizeSerializable.Size), Flags);

                        if (prop != null)
                        {
                            IsFixedSizeSerializable = true;
                            SerializableFixedSize = (System.Int32)prop.GetValue(null)!;
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