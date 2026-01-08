// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Shared.Extensions;

/// <summary>
/// Provides extension methods for working with <see cref="System.Enum"/> types.
/// </summary>
public static class EnumExtensions
{
    /// <summary>
    /// Adds the specified flag to the given enumeration value.
    /// </summary>
    /// <typeparam name="TEnum">The enumeration type, which must be decorated with <see cref="System.FlagsAttribute"/>.</typeparam>
    /// <param name="this">The enumeration value to modify.</param>
    /// <param name="mask">The flag to add.</param>
    /// <returns>A new enumeration value with the specified flag added.</returns>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static TEnum AddFlag<TEnum>(this TEnum @this, TEnum mask)
        where TEnum : unmanaged, System.Enum
    {
#if DEBUG
        if (!typeof(TEnum).IsDefined(typeof(System.FlagsAttribute), false))
        {
            throw new System.ArgumentException(
                $"{typeof(TEnum).Name} must have [Flags] attribute to use AddFlag/RemoveFlag.");
        }
#endif
        System.Int32 size = System.Runtime.CompilerServices.Unsafe.SizeOf<TEnum>();

        if (size == sizeof(System.Byte))
        {
            System.Byte a = System.Runtime.CompilerServices.Unsafe.As<TEnum, System.Byte>(ref @this);
            System.Byte b = System.Runtime.CompilerServices.Unsafe.As<TEnum, System.Byte>(ref mask);
            System.Byte r = (System.Byte)(a | b);
            return System.Runtime.CompilerServices.Unsafe.As<System.Byte, TEnum>(ref r);
        }

        if (size == sizeof(System.UInt16))
        {
            System.UInt16 a = System.Runtime.CompilerServices.Unsafe.As<TEnum, System.UInt16>(ref @this);
            System.UInt16 b = System.Runtime.CompilerServices.Unsafe.As<TEnum, System.UInt16>(ref mask);
            System.UInt16 r = (System.UInt16)(a | b);
            return System.Runtime.CompilerServices.Unsafe.As<System.UInt16, TEnum>(ref r);
        }

        if (size == sizeof(System.UInt32))
        {
            System.UInt32 a = System.Runtime.CompilerServices.Unsafe.As<TEnum, System.UInt32>(ref @this);
            System.UInt32 b = System.Runtime.CompilerServices.Unsafe.As<TEnum, System.UInt32>(ref mask);
            System.UInt32 r = a | b;
            return System.Runtime.CompilerServices.Unsafe.As<System.UInt32, TEnum>(ref r);
        }

        if (size == sizeof(System.UInt64))
        {
            System.UInt64 a = System.Runtime.CompilerServices.Unsafe.As<TEnum, System.UInt64>(ref @this);
            System.UInt64 b = System.Runtime.CompilerServices.Unsafe.As<TEnum, System.UInt64>(ref mask);
            System.UInt64 r = a | b;
            return System.Runtime.CompilerServices.Unsafe.As<System.UInt64, TEnum>(ref r);
        }

        throw new System.NotSupportedException(
            $"Enum underlying type of size {size} is not supported.");
    }

    /// <summary>
    /// Removes the specified flag from the given enumeration value.
    /// </summary>
    /// <typeparam name="TEnum">The enumeration type, which must be decorated with <see cref="System.FlagsAttribute"/>.</typeparam>
    /// <param name="this">The enumeration value to modify.</param>
    /// <param name="mask">The flag to remove.</param>
    /// <returns>A new enumeration value with the specified flag removed.</returns>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static TEnum RemoveFlag<TEnum>(this TEnum @this, TEnum mask)
        where TEnum : unmanaged, System.Enum
    {
#if DEBUG
        if (!typeof(TEnum).IsDefined(typeof(System.FlagsAttribute), false))
        {
            throw new System.ArgumentException(
                $"{typeof(TEnum).Name} must have [Flags] attribute to use AddFlag/RemoveFlag.");
        }
#endif
        System.Int32 size = System.Runtime.CompilerServices.Unsafe.SizeOf<TEnum>();

        if (size == sizeof(System.Byte))
        {
            System.Byte a = System.Runtime.CompilerServices.Unsafe.As<TEnum, System.Byte>(ref @this);
            System.Byte b = System.Runtime.CompilerServices.Unsafe.As<TEnum, System.Byte>(ref mask);
            System.Byte r = (System.Byte)(a & ~b);
            return System.Runtime.CompilerServices.Unsafe.As<System.Byte, TEnum>(ref r);
        }

        if (size == sizeof(System.UInt16))
        {
            System.UInt16 a = System.Runtime.CompilerServices.Unsafe.As<TEnum, System.UInt16>(ref @this);
            System.UInt16 b = System.Runtime.CompilerServices.Unsafe.As<TEnum, System.UInt16>(ref mask);
            System.UInt16 r = (System.UInt16)(a & ~b);
            return System.Runtime.CompilerServices.Unsafe.As<System.UInt16, TEnum>(ref r);
        }

        if (size == sizeof(System.UInt32))
        {
            System.UInt32 a = System.Runtime.CompilerServices.Unsafe.As<TEnum, System.UInt32>(ref @this);
            System.UInt32 b = System.Runtime.CompilerServices.Unsafe.As<TEnum, System.UInt32>(ref mask);
            System.UInt32 r = a & ~b;
            return System.Runtime.CompilerServices.Unsafe.As<System.UInt32, TEnum>(ref r);
        }

        if (size == sizeof(System.UInt64))
        {
            System.UInt64 a = System.Runtime.CompilerServices.Unsafe.As<TEnum, System.UInt64>(ref @this);
            System.UInt64 b = System.Runtime.CompilerServices.Unsafe.As<TEnum, System.UInt64>(ref mask);
            System.UInt64 r = a & ~b;
            return System.Runtime.CompilerServices.Unsafe.As<System.UInt64, TEnum>(ref r);
        }

        throw new System.NotSupportedException(
            $"Enum underlying type of size {size} is not supported.");
    }
}