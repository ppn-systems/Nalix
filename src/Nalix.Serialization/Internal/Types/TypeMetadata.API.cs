namespace Nalix.Serialization.Internal.Types;

internal static partial class TypeMetadata
{
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static bool IsReferenceOrNullable<[
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(PropertyAccess)] T>()
        => Cache<T>.IsReferenceOrNullable;

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
}
