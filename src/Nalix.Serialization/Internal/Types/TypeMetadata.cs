using Nalix.Common.Serialization;

namespace Nalix.Serialization.Internal.Types;

internal static partial class TypeMetadata
{
    private const System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes PropertyAccess =
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicProperties;

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<System.Type, System.Func<bool>> _isRefCache;

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<System.Type, System.Func<int>> _sizeOfFnCache;

    static TypeMetadata()
    {
        System.Type _ = typeof(IFixedSizeSerializable);

        _isRefCache = new();
        _sizeOfFnCache = new();
    }

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

    // Phương thức trợ giúp để xác định nếu một kiểu chứa tham chiếu
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static bool IsReferenceOrContainsReferences(System.Type type)
    {
        System.Func<bool> fn = _isRefCache.GetOrAdd(type, static t =>
        {
            var method = typeof(System.Runtime.CompilerServices.RuntimeHelpers)
                .GetMethod(nameof(System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences))!
                .MakeGenericMethod(t);

            var call = System.Linq.Expressions.Expression.Lambda<System.Func<bool>>(
                System.Linq.Expressions.Expression.Call(null, method));

            return call.Compile();
        });

        return fn();
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static int UnsafeSizeOf(System.Type type)
    {
        System.Func<int> del = _sizeOfFnCache.GetOrAdd(type, static t =>
        {
            var method = typeof(System.Runtime.CompilerServices.Unsafe)
                .GetMethod("SizeOf")!
                .MakeGenericMethod(t);

            var call = System.Linq.Expressions.Expression.Lambda<System.Func<int>>(
                System.Linq.Expressions.Expression.Call(null, method));

            return call.Compile();
        });

        return del();
    }

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

    internal enum TypeKind : byte
    {
        None,
        UnmanagedSZArray,
        FixedSizeSerializable
    }
}
