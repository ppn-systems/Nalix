namespace Notio.Cryptography;

internal static class SpanExtensions
{
    /// <summary>
    /// Performs a non-portable cast of the elements in the span from the specified source type to the specified target type.
    /// </summary>
    /// <typeparam name="TFrom">The type of the elements in the source span.</typeparam>
    /// <typeparam name="TTo">The type of the elements in the target span.</typeparam>
    /// <param name="span">The span containing the elements to be cast.</param>
    /// <returns>A new span of the target type containing the cast elements.</returns>
    /// <exception cref="System.InvalidOperationException">Thrown when the size of TFrom is not equal to the size of TTo.</exception>
    public static System.Span<TTo> NonPortableCast<TFrom, TTo>(this System.Span<TFrom> span)
        where TFrom : unmanaged
        where TTo : unmanaged
    {
        if (System.Runtime.CompilerServices.Unsafe.SizeOf<TFrom>() != System.Runtime.CompilerServices.Unsafe.SizeOf<TTo>())
            throw new System.InvalidOperationException("Size mismatch between TFrom and TTo.");

#if UNSAFE
        ref TFrom fromRef = ref System.Runtime.InteropServices.MemoryMarshal.GetReference(span);
        ref TTo toRef = ref System.Runtime.CompilerServices.Unsafe.As<TFrom, TTo>(ref fromRef);
        return System.Runtime.InteropServices.MemoryMarshal.CreateSpan(ref toRef, span.Length);
#else
        return System.Runtime.InteropServices.MemoryMarshal.Cast<TFrom, TTo>(span);
#endif
    }
}
