namespace Nalix.Shared.Extensions.Primitives;

/// <summary>
/// Provides extension methods for working with <see cref="System.Enum"/> types.
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
    /// <exception cref="System.ArgumentException">
    /// Thrown if the size of <typeparamref name="TEnum"/> and <typeparamref name="TValue"/> are not the same.
    /// </exception>

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static TValue As<TEnum, TValue>(this TEnum @this) where TEnum : System.Enum
    {
        // Ensure that TEnum and TValue have the same size in memory
        if (System.Runtime.CompilerServices.Unsafe.SizeOf<TEnum>() !=
            System.Runtime.CompilerServices.Unsafe.SizeOf<TValue>())
        {
            throw new System.ArgumentException("Size of TEnum and TValue must be the same.", nameof(@this));
        }

        // Perform the conversion using Unsafe.As
        return System.Runtime.CompilerServices.Unsafe.As<TEnum, TValue>(ref @this);
    }

    /// <summary>
    /// Determines whether the specified flag is set in the given enumeration value.
    /// </summary>
    /// <typeparam name="TEnum">The enumeration type, which must be decorated with <see cref="System.FlagsAttribute"/>.</typeparam>
    /// <param name="flags">The enumeration value to check.</param>
    /// <param name="flag">The flag to check for.</param>
    /// <returns><see langword="true"/> if the specified flag is set; otherwise, <see langword="false"/>.</returns>

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean HasFlag<TEnum>(this TEnum flags, TEnum flag)
        where TEnum : struct, System.Enum
        => (System.Convert.ToUInt64(flags) & System.Convert.ToUInt64(flag)) == System.Convert.ToUInt64(flag);

    /// <summary>
    /// Adds the specified flag to the given enumeration value.
    /// </summary>
    /// <typeparam name="TEnum">The enumeration type, which must be decorated with <see cref="System.FlagsAttribute"/>.</typeparam>
    /// <param name="flags">The enumeration value to modify.</param>
    /// <param name="flag">The flag to add.</param>
    /// <returns>A new enumeration value with the specified flag added.</returns>

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static TEnum AddFlag<TEnum>(this TEnum flags, TEnum flag)
        where TEnum : struct, System.Enum
        => (TEnum)System.Enum.ToObject(typeof(TEnum), System.Convert.ToUInt64(flags) | System.Convert.ToUInt64(flag));

    /// <summary>
    /// Removes the specified flag from the given enumeration value.
    /// </summary>
    /// <typeparam name="TEnum">The enumeration type, which must be decorated with <see cref="System.FlagsAttribute"/>.</typeparam>
    /// <param name="flags">The enumeration value to modify.</param>
    /// <param name="flag">The flag to remove.</param>
    /// <returns>A new enumeration value with the specified flag removed.</returns>

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static TEnum RemoveFlag<TEnum>(this TEnum flags, TEnum flag)
        where TEnum : struct, System.Enum
        => (TEnum)System.Enum.ToObject(typeof(TEnum), System.Convert.ToUInt64(flags) & ~System.Convert.ToUInt64(flag));

    /// <summary>
    /// Determines whether the enumeration value is set to none (zero).
    /// </summary>
    /// <typeparam name="TEnum">The enumeration type, which must be decorated with <see cref="System.FlagsAttribute"/>.</typeparam>
    /// <param name="flags">The enumeration value to check.</param>
    /// <returns><see langword="true"/> if no flags are set; otherwise, <see langword="false"/>.</returns>

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean IsNone<TEnum>(this TEnum flags)
        where TEnum : struct, System.Enum
        => System.Convert.ToUInt64(flags) == 0;

    /// <summary>
    /// Determines whether the enumeration value matches the specified required flags
    /// and does not contain any of the excluded flags.
    /// </summary>
    /// <typeparam name="TEnum">The enumeration type, which must be decorated with <see cref="System.FlagsAttribute"/>.</typeparam>
    /// <param name="flags">The enumeration value to check.</param>
    /// <param name="requiredFlags">The flags that must be present.</param>
    /// <param name="excludedFlags">The flags that must not be present.</param>
    /// <returns>
    /// <see langword="true"/> if the enumeration value contains all required flags
    /// and does not contain any excluded flags; otherwise, <see langword="false"/>.
    /// </returns>

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean Matches<TEnum>(this TEnum flags, TEnum requiredFlags, TEnum excludedFlags)
        where TEnum : struct, System.Enum
    {
        System.UInt64 f = System.Convert.ToUInt64(flags);
        return (f & System.Convert.ToUInt64(requiredFlags)) == System.Convert.ToUInt64(requiredFlags) &&
               (f & System.Convert.ToUInt64(excludedFlags)) == 0;
    }
}