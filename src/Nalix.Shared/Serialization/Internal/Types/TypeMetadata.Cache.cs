// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Serialization;

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Benchmarks")]
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
        public static bool IsUnmanaged;
        public static bool IsNullable;
        public static bool IsReference;
        public static bool IsUnmanagedSZArray;
        public static bool IsFixedSizeSerializable;
        public static bool IsCompositeSerializable;

        public static int SerializableFixedSize;
        public static int UnmanagedSZArrayElementSize;

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
                else if (typeof(IFixedSizeSerializable).IsAssignableFrom(type))
                {
                    System.Reflection.PropertyInfo? prop = type.GetProperty(nameof(IFixedSizeSerializable.Size), Flags);

                    if (prop?.GetValue(null) is int size)
                    {
                        IsFixedSizeSerializable = true;
                        SerializableFixedSize = size;
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
