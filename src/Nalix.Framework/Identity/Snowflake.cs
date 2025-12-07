// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Abstractions;
using Nalix.Common.Enums;
using Nalix.Common.Primitives;
using Nalix.Framework.Configuration;
using Nalix.Framework.Options;
using Nalix.Framework.Time;

namespace Nalix.Framework.Identity;

/// <summary>
/// Represents a 56-bit unique identifier composed of a value, machine identifier, and type.
/// Provides methods for creation, decomposition, and conversion to various formats.
/// </summary>
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[System.Runtime.InteropServices.StructLayout(
    System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)]
[System.Diagnostics.DebuggerDisplay("0x{ToUInt56():X14} (T={Type}, M={MachineId})")]
public readonly partial struct Snowflake : ISnowflake
{
    #region Const

    /// <summary>
    /// The size in bytes of the <see cref="Snowflake"/> structure.
    /// </summary>
    public const System.Byte Size = 7;

    private readonly UInt56 __combined;
    private static readonly System.UInt16 __machineId = ConfigurationManager.Instance.Get<SnowflakeOptions>().MachineId;

    #endregion Const

    #region Decomposition

    /// <summary>
    /// Gets the 32-bit value component.
    /// </summary>
    public System.UInt32 Value
    {
        get
        {
            __combined.Decompose(out System.Byte _, out System.UInt16 _, out System.UInt32 value);
            return value;
        }
    }

    /// <summary>
    /// Gets the 16-bit machine identifier component.
    /// </summary>
    public System.UInt16 MachineId
    {
        get
        {
            __combined.Decompose(out System.Byte _, out System.UInt16 machineId, out System.UInt32 _);
            return machineId;
        }
    }

    /// <summary
    /// >Gets the 8-bit type component.
    /// </summary>
    public SnowflakeType Type
    {
        get
        {
            __combined.Decompose(out System.Byte type, out System.UInt16 _, out System.UInt32 _);
            return (SnowflakeType)type;
        }
    }

    #endregion Decomposition

    #region Public Properties

    /// <summary>
    /// Gets an empty <see cref="Snowflake"/> instance with all components set to zero.
    /// </summary>
    public static Snowflake Empty => new(0, 0, 0);

    /// <inheritdoc/>
    public System.Boolean IsEmpty => __combined == 0;

    #endregion Public Properties

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="Snowflake"/> struct.
    /// </summary>
    /// <param name="value">The main identifier value.</param>
    /// <param name="machineId">The machine identifier.</param>
    /// <param name="type">The identifier type.</param>
    [System.Diagnostics.DebuggerHidden]
    private Snowflake(System.UInt32 value, System.UInt16 machineId, SnowflakeType type)
    {
        __combined = UInt56.FromParts((System.Byte)type, machineId, value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Snowflake"/> struct from a <see cref="UInt56"/> value.
    /// </summary>
    /// <param name="uInt56">The 56-bit unsigned integer representing the combined identifier value.</param>
    public Snowflake(UInt56 uInt56)
    {
        __combined = uInt56;
    }

    #endregion Constructors

    #region Factory Methods

    /// <summary>
    /// Creates a new <see cref="Snowflake"/> from a <see cref="UInt56"/> value.
    /// </summary>
    /// <param name="uInt56">The 56-bit unsigned integer representing the combined identifier value.</param>
    /// <returns>A new <see cref="Snowflake"/> instance.</returns>
    [System.Diagnostics.DebuggerHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static Snowflake NewId(UInt56 uInt56) => new(uInt56);

    /// <summary>
    /// Creates a new <see cref="Snowflake"/> with the specified components.
    /// </summary>
    /// <param name="value">The main identifier value.</param>
    /// <param name="machineId">The machine identifier.</param>
    /// <param name="type">The identifier type.</param>
    /// <returns>A new <see cref="Snowflake"/> instance.</returns>
    /// <example>
    /// <code>
    /// var id = Identifier.Generate(12345, 1001, IdentifierType.USER);
    /// Console.WriteLine(id.ToString()); // Outputs string representation
    /// </code>
    /// </example>
    [System.Diagnostics.DebuggerHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static Snowflake NewId(System.UInt32 value, System.UInt16 machineId, SnowflakeType type) => new(value, machineId, type);

    /// <summary>
    /// Creates a new <see cref="Snowflake"/> with the specified components.
    /// </summary>
    /// <param name="type">The identifier type.</param>
    /// <param name="machineId">The machine identifier.</param>
    /// <returns>A new <see cref="Snowflake"/> instance.</returns>
    /// <example>
    /// <code>
    /// var id = Identifier.Generate(IdentifierType.SYSTEM);
    /// Console.WriteLine(id.ToString());
    /// </code>
    /// </example>
    [System.Diagnostics.DebuggerHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static Snowflake NewId(SnowflakeType type, System.UInt16 machineId = 1) => new(Clock.UnixSecondsNowUInt32(), machineId, type);

    /// <summary>
    /// Creates a new <see cref="Snowflake"/> with the specified components.
    /// </summary>
    /// <param name="type">The identifier type.</param>
    /// <returns>A new <see cref="Snowflake"/> instance.</returns>
    /// <example>
    /// <code>
    /// var id = Identifier.Generate(IdentifierType.SYSTEM);
    /// Console.WriteLine(id.ToString()); // Outputs string representation
    /// </code>
    /// </example>
    [System.Diagnostics.DebuggerHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static Snowflake NewId(SnowflakeType type) => new(Clock.UnixSecondsNowUInt32(), __machineId, type);

    #endregion Factory Methods

    #region Override

    /// <summary>
    /// Returns the hash code for this identifier.
    /// </summary>
    /// <returns>A 32-bit signed integer hash code.</returns>
    /// <remarks>
    /// The hash code is computed efficiently using all components of the identifier
    /// and is suitable for use in hash-based collections like <see cref="System.Collections.Generic.Dictionary{TKey,TValue}"/>.
    /// </remarks>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public override System.Int32 GetHashCode() => ToUInt56().GetHashCode();

    /// <summary>
    /// Determines whether this identifier is equal to the specified object.
    /// </summary>
    /// <param name="obj">The object to compare with this instance.</param>
    /// <returns>
    /// <c>true</c> if the object is a <see cref="Snowflake"/> and is equal to this instance;
    /// otherwise, <c>false</c>.
    /// </returns>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public override System.Boolean Equals(System.Object? obj) => obj is Snowflake other && Equals(other);

    /// <summary>
    /// Returns the Hex string representation of this identifier.
    /// </summary>
    /// <returns>A Hex encoded string representing this identifier.</returns>
    [System.Diagnostics.DebuggerHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public override System.String ToString()
    {
        System.Span<System.Byte> buffer = stackalloc System.Byte[7];
        _ = TryWriteBytes(buffer, out _);
        return System.Convert.ToHexString(buffer);
    }

    #endregion Override
}
