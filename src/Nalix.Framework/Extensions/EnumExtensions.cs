// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace Nalix.Framework.Extensions;

/// <summary>
/// Provides extension methods for working with <see cref="Enum"/> types.
/// </summary>
public static class EnumExtensions
{
    /// <summary>
    /// Adds the specified flag to the given enumeration value.
    /// </summary>
    /// <typeparam name="TEnum">The enumeration type, which must be decorated with <see cref="FlagsAttribute"/>.</typeparam>
    /// <param name="this">The enumeration value to modify.</param>
    /// <param name="mask">The flag to add.</param>
    /// <returns>A new enumeration value with the specified flag added.</returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="NotSupportedException"></exception>
    [Pure]
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TEnum AddFlag<TEnum>(this TEnum @this, TEnum mask)
        where TEnum : unmanaged, Enum
    {
#if DEBUG
        if (!typeof(TEnum).IsDefined(typeof(FlagsAttribute), false))
        {
            throw new ArgumentException(
                $"{typeof(TEnum).Name} must have [Flags] attribute to use AddFlag/RemoveFlag.");
        }
#endif
        int size = Unsafe.SizeOf<TEnum>();

        if (size == sizeof(byte))
        {
            byte a = Unsafe.As<TEnum, byte>(ref @this);
            byte b = Unsafe.As<TEnum, byte>(ref mask);
            byte r = (byte)(a | b);
            return Unsafe.As<byte, TEnum>(ref r);
        }

        if (size == sizeof(ushort))
        {
            ushort a = Unsafe.As<TEnum, ushort>(ref @this);
            ushort b = Unsafe.As<TEnum, ushort>(ref mask);
            ushort r = (ushort)(a | b);
            return Unsafe.As<ushort, TEnum>(ref r);
        }

        if (size == sizeof(uint))
        {
            uint a = Unsafe.As<TEnum, uint>(ref @this);
            uint b = Unsafe.As<TEnum, uint>(ref mask);
            uint r = a | b;
            return Unsafe.As<uint, TEnum>(ref r);
        }

        if (size == sizeof(ulong))
        {
            ulong a = Unsafe.As<TEnum, ulong>(ref @this);
            ulong b = Unsafe.As<TEnum, ulong>(ref mask);
            ulong r = a | b;
            return Unsafe.As<ulong, TEnum>(ref r);
        }

        throw new NotSupportedException(
            $"Enum underlying type of size {size} is not supported.");
    }

    /// <summary>
    /// Removes the specified flag from the given enumeration value.
    /// </summary>
    /// <typeparam name="TEnum">The enumeration type, which must be decorated with <see cref="FlagsAttribute"/>.</typeparam>
    /// <param name="this">The enumeration value to modify.</param>
    /// <param name="mask">The flag to remove.</param>
    /// <returns>A new enumeration value with the specified flag removed.</returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="NotSupportedException"></exception>
    [Pure]
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TEnum RemoveFlag<TEnum>(this TEnum @this, TEnum mask)
        where TEnum : unmanaged, Enum
    {
#if DEBUG
        if (!typeof(TEnum).IsDefined(typeof(FlagsAttribute), false))
        {
            throw new ArgumentException(
                $"{typeof(TEnum).Name} must have [Flags] attribute to use AddFlag/RemoveFlag.");
        }
#endif
        int size = Unsafe.SizeOf<TEnum>();

        if (size == sizeof(byte))
        {
            byte a = Unsafe.As<TEnum, byte>(ref @this);
            byte b = Unsafe.As<TEnum, byte>(ref mask);
            byte r = (byte)(a & ~b);
            return Unsafe.As<byte, TEnum>(ref r);
        }

        if (size == sizeof(ushort))
        {
            ushort a = Unsafe.As<TEnum, ushort>(ref @this);
            ushort b = Unsafe.As<TEnum, ushort>(ref mask);
            ushort r = (ushort)(a & ~b);
            return Unsafe.As<ushort, TEnum>(ref r);
        }

        if (size == sizeof(uint))
        {
            uint a = Unsafe.As<TEnum, uint>(ref @this);
            uint b = Unsafe.As<TEnum, uint>(ref mask);
            uint r = a & ~b;
            return Unsafe.As<uint, TEnum>(ref r);
        }

        if (size == sizeof(ulong))
        {
            ulong a = Unsafe.As<TEnum, ulong>(ref @this);
            ulong b = Unsafe.As<TEnum, ulong>(ref mask);
            ulong r = a & ~b;
            return Unsafe.As<ulong, TEnum>(ref r);
        }

        throw new NotSupportedException(
            $"Enum underlying type of size {size} is not supported.");
    }
}
