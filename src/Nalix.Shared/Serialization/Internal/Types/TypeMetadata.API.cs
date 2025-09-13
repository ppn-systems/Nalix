// Copyright (c) 2025 PPN Corporation. All rights reserved.

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Benchmarks")]
#endif

namespace Nalix.Shared.Serialization.Internal.Types;

/// <summary>
/// Provides metadata operations for serialization types.
/// </summary>
internal static partial class TypeMetadata
{
    /// <summary>
    /// Retrieves the size, in bytes, of the specified unmanaged type.
    /// </summary>
    /// <typeparam name="T">The unmanaged type to evaluate.</typeparam>
    /// <returns>The size of the type in bytes.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Int32 SizeOf<T>()
        => System.Runtime.CompilerServices.Unsafe.SizeOf<T>();

    /// <summary>
    /// Determines whether the specified type is unmanaged.
    /// </summary>
    /// <typeparam name="T">The type to check.</typeparam>
    /// <returns>True if the type is unmanaged; otherwise, false.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean IsUnmanaged<[
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(PropertyAccess)] T>()
        => Cache<T>.IsUnmanaged;

    /// <summary>
    /// Determines whether the specified type is unmanaged by examining its structure and fields.
    /// </summary>
    /// <param name="type">The type to check for unmanaged status.</param>
    /// <returns>True if the type is unmanaged; otherwise, false.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="type"/> is null.</exception>
    public static System.Boolean IsUnmanaged(System.Type type)
    {
        try
        {
            return type.IsValueType &&
                   System.Runtime.InteropServices.Marshal.SizeOf(type) > 0 &&
                   System.Linq.Enumerable.All(
                       type.GetFields(System.Reflection.BindingFlags.Instance |
                                      System.Reflection.BindingFlags.NonPublic |
                                      System.Reflection.BindingFlags.Public),
                       f => IsUnmanaged(f.FieldType));
        }
        catch { return false; }
    }

    /// <summary>
    /// Determines whether the specified type is nullable.
    /// </summary>
    /// <typeparam name="T">The type to check.</typeparam>
    /// <returns>True if the type is nullable; otherwise, false.</returns>
    public static System.Boolean IsNullable<
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(PropertyAccess)] T>()
        => Cache<T>.IsNullable;

    /// <summary>
    /// Determines whether the type is a reference type or nullable.
    /// </summary>
    /// <typeparam name="T">The type to check.</typeparam>
    /// <returns>True if the type is a reference type or nullable; otherwise, false.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean IsReferenceOrNullable<[
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(PropertyAccess)] T>()
        => Cache<T>.IsReference || Cache<T>.IsNullable;

    /// <summary>
    /// Attempts to retrieve the fixed or unmanaged size of a type.
    /// </summary>
    /// <typeparam name="T">The type to evaluate.</typeparam>
    /// <param name="size">The fixed or unmanaged size of the type.</param>
    /// <returns>The corresponding <see cref="TypeKind"/> value.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static TypeKind TryGetFixedOrUnmanagedSize<
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(PropertyAccess)] T>(out System.Int32 size)
    {
        if (Cache<T>.IsUnmanagedSZArray)
        {
            size = Cache<T>.UnmanagedSZArrayElementSize;
            return TypeKind.UnmanagedSZArray;
        }
        else
        {
            if (Cache<T>.IsFixedSizeSerializable)
            {
                size = Cache<T>.SerializableFixedSize;
                return TypeKind.FixedSizeSerializable;
            }
        }

        size = 0;
        return TypeKind.None;
    }

    /// <summary>
    /// Determines whether a given type is an anonymous type.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>True if the type is anonymous; otherwise, false.</returns>
    public static System.Boolean IsAnonymous(System.Type type)
    {
        // Kiểu ẩn danh thường không có namespace
        System.Boolean hasNoNamespace = type.Namespace == null;

        // Kiểu ẩn danh thường là sealed (không thể kế thừa)
        System.Boolean isSealed = type.IsSealed;

        // Tên kiểu ẩn danh thường bắt đầu bằng các chuỗi đặc biệt do trình biên dịch tạo ra
        System.Boolean nameIndicatesAnonymous =
            type.Name.StartsWith("<>f__AnonymousType", System.StringComparison.Ordinal) ||
            type.Name.StartsWith("<>__AnonType", System.StringComparison.Ordinal) ||
            type.Name.StartsWith("VB$AnonymousType_", System.StringComparison.Ordinal); // cho VB.NET

        // Kiểu ẩn danh được đánh dấu bằng thuộc tính CompilerGeneratedAttribute
        System.Boolean isCompilerGenerated = type.IsDefined(
            typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), inherit: false);

        // Kết luận: là kiểu ẩn danh nếu thỏa mãn tất cả điều kiện trên
        return hasNoNamespace && isSealed && nameIndicatesAnonymous && isCompilerGenerated;
    }
}