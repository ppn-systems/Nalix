// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using Nalix.Common.Serialization;

#if DEBUG
[assembly: InternalsVisibleTo("Nalix.Shared.Tests")]
[assembly: InternalsVisibleTo("Nalix.Shared.Benchmarks")]
#endif

namespace Nalix.Shared.Serialization.Internal.Types;

internal static partial class TypeMetadata
{
    private const BindingFlags Flags =
        BindingFlags.Static |
        BindingFlags.Public |
        BindingFlags.Instance |
        BindingFlags.NonPublic |
        BindingFlags.FlattenHierarchy;

    public const DynamicallyAccessedMemberTypes PropertyAccess =
        DynamicallyAccessedMemberTypes.PublicProperties |
        DynamicallyAccessedMemberTypes.NonPublicProperties;

    private static class Cache<[DynamicallyAccessedMembers(PropertyAccess)] T>
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
            Type type = typeof(T);

            try
            {
                IsReference = !type.IsValueType;
                IsNullable = Nullable.GetUnderlyingType(type) != null;
                IsUnmanaged = !RuntimeHelpers.IsReferenceOrContainsReferences<T>();

                if (type.IsSZArray)
                {
                    Type? elementType = type.GetElementType();
                    if (elementType != null && !IsReferenceOrContainsReferences(elementType))
                    {
                        IsUnmanagedSZArray = true;
                        UnmanagedSZArrayElementSize = UnsafeSizeOf(elementType);
                    }
                }
                else if (typeof(IFixedSizeSerializable).IsAssignableFrom(type))
                {
                    PropertyInfo? prop = type.GetProperty(nameof(IFixedSizeSerializable.Size), Flags);

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
