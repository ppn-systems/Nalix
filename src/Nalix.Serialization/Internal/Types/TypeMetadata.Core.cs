using Nalix.Common.Serialization;

namespace Nalix.Serialization.Internal.Types;

internal static partial class TypeMetadata
{
    public const System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes PropertyAccess =
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
}
