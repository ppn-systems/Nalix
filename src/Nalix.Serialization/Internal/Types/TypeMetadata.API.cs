namespace Nalix.Serialization.Internal.Types;

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
    public static int SizeOf<T>()
        => System.Runtime.CompilerServices.Unsafe.SizeOf<T>();

    /// <summary>
    /// Determines whether the specified type is unmanaged.
    /// </summary>
    /// <typeparam name="T">The type to check.</typeparam>
    /// <returns>True if the type is unmanaged; otherwise, false.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static bool IsUnmanaged<[
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(PropertyAccess)] T>()
        => Cache<T>.IsUnmanaged;

    /// <summary>
    /// Determines whether the specified type is nullable.
    /// </summary>
    /// <typeparam name="T">The type to check.</typeparam>
    /// <returns>True if the type is nullable; otherwise, false.</returns>
    public static bool IsNullable<
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(PropertyAccess)] T>()
        => Cache<T>.IsNullable;

    /// <summary>
    /// Determines whether the type is a reference type or nullable.
    /// </summary>
    /// <typeparam name="T">The type to check.</typeparam>
    /// <returns>True if the type is a reference type or nullable; otherwise, false.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static bool IsReferenceOrNullable<[
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
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(PropertyAccess)] T>(out int size)
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
    public static bool IsAnonymous(System.Type type)
    {
        // Kiểu ẩn danh thường không có namespace
        bool hasNoNamespace = type.Namespace == null;

        // Kiểu ẩn danh thường là sealed (không thể kế thừa)
        bool isSealed = type.IsSealed;

        // Tên kiểu ẩn danh thường bắt đầu bằng các chuỗi đặc biệt do trình biên dịch tạo ra
        bool nameIndicatesAnonymous =
            type.Name.StartsWith("<>f__AnonymousType", System.StringComparison.Ordinal) ||
            type.Name.StartsWith("<>__AnonType", System.StringComparison.Ordinal) ||
            type.Name.StartsWith("VB$AnonymousType_", System.StringComparison.Ordinal); // cho VB.NET

        // Kiểu ẩn danh được đánh dấu bằng thuộc tính CompilerGeneratedAttribute
        bool isCompilerGenerated = type.IsDefined(
            typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), inherit: false);

        // Kết luận: là kiểu ẩn danh nếu thỏa mãn tất cả điều kiện trên
        return hasNoNamespace && isSealed && nameIndicatesAnonymous && isCompilerGenerated;
    }

    /// <summary>
    /// Retrieves an array of serializable properties for the specified type.
    /// </summary>
    /// <param name="type">The type for which to retrieve serializable properties.</param>
    /// <returns>An array of <see cref="System.Reflection.PropertyInfo"/> representing the serializable properties of the type.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Trimming",
        "IL2111:Method with parameters or return value with `DynamicallyAccessedMembersAttribute` is accessed via reflection. " +
        "Trimmer can't guarantee availability of the requirements of the method.", Justification = "<Pending>")]
    public static System.Reflection.PropertyInfo[] GetProperties(
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(PropertyAccess)] System.Type type)
        => _cacheProperty.GetOrAdd(type, ComputeSerializableProperties);
}
