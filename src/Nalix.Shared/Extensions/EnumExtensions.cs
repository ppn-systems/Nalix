// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

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
    /// <exception cref="System.ArgumentException"></exception>
    /// <exception cref="System.NotSupportedException"></exception>
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
        int size = System.Runtime.CompilerServices.Unsafe.SizeOf<TEnum>();

        if (size == sizeof(byte))
        {
            byte a = System.Runtime.CompilerServices.Unsafe.As<TEnum, byte>(ref @this);
            byte b = System.Runtime.CompilerServices.Unsafe.As<TEnum, byte>(ref mask);
            byte r = (byte)(a | b);
            return System.Runtime.CompilerServices.Unsafe.As<byte, TEnum>(ref r);
        }

        if (size == sizeof(ushort))
        {
            ushort a = System.Runtime.CompilerServices.Unsafe.As<TEnum, ushort>(ref @this);
            ushort b = System.Runtime.CompilerServices.Unsafe.As<TEnum, ushort>(ref mask);
            ushort r = (ushort)(a | b);
            return System.Runtime.CompilerServices.Unsafe.As<ushort, TEnum>(ref r);
        }

        if (size == sizeof(uint))
        {
            uint a = System.Runtime.CompilerServices.Unsafe.As<TEnum, uint>(ref @this);
            uint b = System.Runtime.CompilerServices.Unsafe.As<TEnum, uint>(ref mask);
            uint r = a | b;
            return System.Runtime.CompilerServices.Unsafe.As<uint, TEnum>(ref r);
        }

        if (size == sizeof(ulong))
        {
            ulong a = System.Runtime.CompilerServices.Unsafe.As<TEnum, ulong>(ref @this);
            ulong b = System.Runtime.CompilerServices.Unsafe.As<TEnum, ulong>(ref mask);
            ulong r = a | b;
            return System.Runtime.CompilerServices.Unsafe.As<ulong, TEnum>(ref r);
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
    /// <exception cref="System.ArgumentException"></exception>
    /// <exception cref="System.NotSupportedException"></exception>
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
        int size = System.Runtime.CompilerServices.Unsafe.SizeOf<TEnum>();

        if (size == sizeof(byte))
        {
            byte a = System.Runtime.CompilerServices.Unsafe.As<TEnum, byte>(ref @this);
            byte b = System.Runtime.CompilerServices.Unsafe.As<TEnum, byte>(ref mask);
            byte r = (byte)(a & ~b);
            return System.Runtime.CompilerServices.Unsafe.As<byte, TEnum>(ref r);
        }

        if (size == sizeof(ushort))
        {
            ushort a = System.Runtime.CompilerServices.Unsafe.As<TEnum, ushort>(ref @this);
            ushort b = System.Runtime.CompilerServices.Unsafe.As<TEnum, ushort>(ref mask);
            ushort r = (ushort)(a & ~b);
            return System.Runtime.CompilerServices.Unsafe.As<ushort, TEnum>(ref r);
        }

        if (size == sizeof(uint))
        {
            uint a = System.Runtime.CompilerServices.Unsafe.As<TEnum, uint>(ref @this);
            uint b = System.Runtime.CompilerServices.Unsafe.As<TEnum, uint>(ref mask);
            uint r = a & ~b;
            return System.Runtime.CompilerServices.Unsafe.As<uint, TEnum>(ref r);
        }

        if (size == sizeof(ulong))
        {
            ulong a = System.Runtime.CompilerServices.Unsafe.As<TEnum, ulong>(ref @this);
            ulong b = System.Runtime.CompilerServices.Unsafe.As<TEnum, ulong>(ref mask);
            ulong r = a & ~b;
            return System.Runtime.CompilerServices.Unsafe.As<ulong, TEnum>(ref r);
        }

        throw new System.NotSupportedException(
            $"Enum underlying type of size {size} is not supported.");
    }
}
