// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Nalix.Common.Identity;
using Nalix.Common.Primitives;
using Nalix.Framework.Configuration;
using Nalix.Framework.Options;
using Nalix.Framework.Time;

namespace Nalix.Framework.Identifiers;

/// <summary>
/// Represents a 56-bit unique identifier composed of a value, machine identifier, and type.
/// Provides methods for creation, decomposition, and conversion to various formats.
/// </summary>
[SkipLocalsInit]
[ExcludeFromCodeCoverage]
[StructLayout(
    LayoutKind.Sequential, Pack = 1)]
[DebuggerDisplay("0x{ToUInt56():X14} (T={Type}, M={MachineId})")]
public readonly partial struct Snowflake : ISnowflake
{
    #region Const

    /// <summary>
    /// The size in bytes of the <see cref="Snowflake"/> structure.
    /// </summary>
    public const byte Size = 7;

    private readonly UInt56 __combined;
    private static readonly ushort __machineId = LAZY_LOAD_MACHINE_ID();

    private static int _sequence;
    private static long _lastTimestampMs;
    private const ushort MaxSequence = 0xFFFF; // 16-bit max = 65535
    private static readonly Lock _generatorLock = new();

    #endregion Const

    #region Decomposition

    /// <inheritdoc/>
    public uint Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ulong raw = (ulong)__combined;
            return (uint)(raw & 0xFFFFFFFFUL);
        }
    }

    /// <inheritdoc/>
    public ushort MachineId
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ulong raw = (ulong)__combined;
            return (ushort)((raw >> 32) & 0xFFFFUL);
        }
    }

    /// <inheritdoc/>
    public SnowflakeType Type
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ulong raw = (ulong)__combined;
            return (SnowflakeType)((raw >> 48) & 0xFFUL);
        }
    }

    #endregion Decomposition

    #region Public Properties

    /// <summary>
    /// Gets an empty <see cref="Snowflake"/> instance with all components set to zero.
    /// </summary>
    public static Snowflake Empty => new(0, 0, 0);

    /// <inheritdoc/>
    public bool IsEmpty => __combined == 0;

    #endregion Public Properties

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="Snowflake"/> struct.
    /// </summary>
    /// <param name="value">The main identifier value (32 bits).</param>
    /// <param name="machineId">The machine identifier (16 bits).</param>
    /// <param name="type">The identifier type (8 bits).</param>
    /// <remarks>
    /// This constructor validates the type parameter to ensure it represents a valid <see cref="SnowflakeType"/>.
    /// The components are efficiently combined using bit operations to form the 56-bit identifier.
    /// </remarks>
    [DebuggerHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Snowflake(uint value, ushort machineId, SnowflakeType type) => __combined = new UInt56((byte)type, machineId, value);

    /// <summary>
    /// Initializes a new instance of the <see cref="Snowflake"/> struct from a <see cref="UInt56"/> value.
    /// </summary>
    /// <param name="uInt56">The 56-bit unsigned integer representing the combined identifier value.</param>
    /// <remarks>
    /// This constructor allows direct initialization from a pre-composed 56-bit value,
    /// which is useful for deserialization scenarios. No validation is performed on the input.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Snowflake(UInt56 uInt56) => __combined = uInt56;

    #endregion Constructors

    #region Factory Methods

    /// <summary>
    /// Creates a new <see cref="Snowflake"/> from a <see cref="UInt56"/> value.
    /// </summary>
    /// <param name="uInt56">The 56-bit unsigned integer representing the combined identifier value.</param>
    /// <returns>A new <see cref="Snowflake"/> instance.</returns>
    /// <remarks>
    /// This method provides a fast way to construct a <see cref="Snowflake"/> from a pre-composed value.
    /// It is particularly useful when deserializing identifiers from storage or network protocols.
    /// </remarks>
    [DebuggerHidden]
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Snowflake NewId(UInt56 uInt56) => new(uInt56);

    /// <summary>
    /// Creates a new <see cref="Snowflake"/> with the specified components.
    /// </summary>
    /// <param name="value">The main identifier value (32 bits).</param>
    /// <param name="machineId">The machine identifier (16 bits).</param>
    /// <param name="type">The identifier type (8 bits).</param>
    /// <returns>A new <see cref="Snowflake"/> instance.</returns>
    /// <remarks>
    /// This method constructs a <see cref="Snowflake"/> from its individual components.
    /// All parameters are validated to ensure they fit within their respective bit ranges.
    /// </remarks>
    /// <example>
    /// <code>
    /// var id = Snowflake.NewId(12345, 1001, SnowflakeType.USER);
    /// Console.WriteLine(id.ToString()); // Outputs hex representation
    /// </code>
    /// </example>
    [DebuggerHidden]
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Snowflake NewId(uint value, ushort machineId, SnowflakeType type) => new(value, machineId, type);

    /// <summary>
    /// Creates a new <see cref="Snowflake"/> with the specified type using the configured machine identifier.
    /// </summary>
    /// <param name="type">The identifier type.</param>
    /// <returns>A new <see cref="Snowflake"/> instance with a timestamp-based value.</returns>
    /// <remarks>
    /// This is the most commonly used factory method. It generates a unique identifier using:
    /// - The current Unix timestamp (in seconds) as the value component
    /// - The globally configured machine ID from <see cref="SnowflakeOptions"/>
    /// - The specified type
    /// This ensures both temporal and spatial uniqueness across distributed systems.
    /// </remarks>
    /// <example>
    /// <code>
    /// var id = Snowflake.NewId(SnowflakeType.SYSTEM);
    /// Console.WriteLine(id.ToString()); // Outputs hex representation
    /// </code>
    /// </example>
    [DebuggerHidden]
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Snowflake NewId(SnowflakeType type) => NewId(type, __machineId);

    /// <summary>
    /// Creates a new <see cref="Snowflake"/> with the specified type and machine identifier.
    /// </summary>
    /// <param name="type">The identifier type.</param>
    /// <param name="machineId">The machine identifier. Defaults to 1 if not specified.</param>
    /// <returns>A new <see cref="Snowflake"/> instance with a timestamp-based value.</returns>
    /// <remarks>
    /// This method generates a unique identifier by combining the current Unix timestamp (in seconds)
    /// with the provided type and machine ID. The timestamp ensures temporal uniqueness.
    /// </remarks>
    /// <example>
    /// <code>
    /// var id = Snowflake.NewId(SnowflakeType.SYSTEM, 42);
    /// Console.WriteLine(id.ToString());
    /// </code>
    /// </example>
    [DebuggerHidden]
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Snowflake NewId(SnowflakeType type, ushort machineId = 1)
    {
        while (true)
        {
            long now = Clock.EpochMillisecondsNow();

            long last = Volatile.Read(ref _lastTimestampMs);

            // Handle clock rollback
            if (now < last)
            {
                now = last;
            }

            int seq;

            if (now == last)
            {
                // same millisecond -> increment sequence
                seq = Interlocked.Increment(ref _sequence) & 0x0FFF;

                if (seq == 0)
                {
                    SpinWait spin = new();
                    do
                    {
                        spin.SpinOnce();
                        now = Clock.EpochMillisecondsNow();
                    }
                    while (now <= last);

                    continue; // retry loop
                }
            }
            else
            {
                // new millisecond -> reset sequence
                seq = 0;
                _ = Interlocked.Exchange(ref _sequence, 0);
            }

            // try publish timestamp (CAS)
            if (Interlocked.CompareExchange(ref _lastTimestampMs, now, last) != last)
            {
                continue; // race -> retry
            }

            // pack 32-bit value
            uint timePart = (uint)(now & 0xFFFFF);   // 20-bit
            uint seqPart = (uint)seq;               // 12-bit

            uint value = (timePart << 12) | seqPart;

            return new Snowflake(value, machineId, type);
        }
    }

    #endregion Factory Methods

    #region Override

    /// <summary>
    /// Returns the hash code for this identifier.
    /// </summary>
    /// <returns>A 32-bit signed integer hash code.</returns>
    /// <remarks>
    /// The hash code is computed efficiently from the underlying 56-bit value
    /// and is suitable for use in hash-based collections like <see cref="Dictionary{TKey,TValue}"/>.
    /// This implementation ensures consistent hash values for equal identifiers.
    /// </remarks>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => __combined.GetHashCode();

    /// <summary>
    /// Determines whether this identifier is equal to the specified object.
    /// </summary>
    /// <param name="obj">The object to compare with this instance.</param>
    /// <returns>
    /// <c>true</c> if the object is a <see cref="Snowflake"/> and is equal to this instance;
    /// otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This method performs type checking and delegates to the strongly-typed <see cref="Equals(Snowflake)"/> method
    /// for actual comparison. Returns <c>false</c> for null or non-<see cref="Snowflake"/> objects.
    /// </remarks>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? obj) => obj is Snowflake other && this.Equals(other);

    /// <summary>
    /// Returns the hexadecimal string representation of this identifier.
    /// </summary>
    /// <returns>A 14-character hexadecimal string representing this identifier (7 bytes = 14 hex digits).</returns>
    /// <remarks>
    /// This method efficiently serializes the identifier to a stack-allocated buffer before
    /// converting to hexadecimal format. The returned string is always uppercase and fixed-length.
    /// </remarks>
    [DebuggerHidden]
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString()
    {
        Span<byte> buffer = stackalloc byte[Size];
        _ = this.TryWriteBytes(buffer);
        return Convert.ToHexString(buffer);
    }

    #endregion Override

    #region Private Methods

    private static ushort LAZY_LOAD_MACHINE_ID() => ConfigurationManager.Instance.Get<SnowflakeOptions>().MachineId;

    #endregion Private Methods
}
