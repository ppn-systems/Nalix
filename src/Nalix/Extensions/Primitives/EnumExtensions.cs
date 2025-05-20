using System;
using System.Runtime.CompilerServices;

namespace Nalix.Extensions.Primitives;

/// <summary>
/// Provides extension methods for working with <see cref="Enum"/> types.
/// </summary>
public static class EnumExtensions
{
    /// <summary>
    /// Converts the value of an enum to a different type, ensuring that the sizes of the two types are the same.
    /// </summary>
    /// <typeparam name="TEnum">The enum type to convert from.</typeparam>
    /// <typeparam name="TValue">The type to convert to.</typeparam>
    /// <param name="this">The enum value to be converted.</param>
    /// <returns>The converted value in the specified type <typeparamref name="TValue"/>.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown if the size of <typeparamref name="TEnum"/> and <typeparamref name="TValue"/> are not the same.
    /// </exception>

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TValue As<TEnum, TValue>(this TEnum @this) where TEnum : Enum
    {
        // Ensure that TEnum and TValue have the same size in memory
        if (Unsafe.SizeOf<TEnum>() != Unsafe.SizeOf<TValue>())
            throw new ArgumentException("Size of TEnum and TValue must be the same.", nameof(@this));

        // Perform the conversion using Unsafe.As
        return Unsafe.As<TEnum, TValue>(ref @this);
    }
}
