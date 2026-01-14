// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Core.Abstractions;
using Nalix.Common.Core.Enums;
using Nalix.Common.Core.Primitives;
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

    private static System.UInt16 _sequence = 0;
    private static System.Int64 _lastTimestampMs = 0;
    private const System.UInt16 MaxSequence = 0xFFFF; // 16-bit max = 65535
    private static readonly System.Threading.Lock _generatorLock = new();

    #endregion Const

    #region Decomposition

    /// <summary>
    /// Gets the 32-bit value component.
    /// </summary>
    /// <remarks>
    /// Extracts the lower 32 bits of the identifier, representing the main value.
    /// This operation is optimized for performance using direct bit manipulation.
    /// </remarks>
    public System.UInt32 Value
    {
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        get
        {
            System.UInt64 raw = (System.UInt64)__combined;
            return (System.UInt32)(raw & 0xFFFFFFFFUL);
        }
    }

    /// <summary>
    /// Gets the 16-bit machine identifier component.
    /// </summary>
    /// <remarks>
    /// Extracts bits 32-47 of the identifier, representing the machine ID.
    /// This operation is optimized for performance using direct bit manipulation.
    /// </remarks>
    public System.UInt16 MachineId
    {
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        get
        {
            System.UInt64 raw = (System.UInt64)__combined;
            return (System.UInt16)((raw >> 32) & 0xFFFFUL);
        }
    }

    /// <summary>
    /// Gets the 8-bit type component.
    /// </summary>
    /// <remarks>
    /// Extracts bits 48-55 of the identifier, representing the snowflake type.
    /// This operation is optimized for performance using direct bit manipulation.
    /// </remarks>
    public SnowflakeType Type
    {
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        get
        {
            System.UInt64 raw = (System.UInt64)__combined;
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
    public System.Boolean IsEmpty => __combined == 0;

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
    [System.Diagnostics.DebuggerHidden]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private Snowflake(System.UInt32 value, System.UInt16 machineId, SnowflakeType type) => __combined = new UInt56((System.Byte)type, machineId, value);

    /// <summary>
    /// Initializes a new instance of the <see cref="Snowflake"/> struct from a <see cref="UInt56"/> value.
    /// </summary>
    /// <param name="uInt56">The 56-bit unsigned integer representing the combined identifier value.</param>
    /// <remarks>
    /// This constructor allows direct initialization from a pre-composed 56-bit value,
    /// which is useful for deserialization scenarios. No validation is performed on the input.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
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
    [System.Diagnostics.DebuggerHidden]
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
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
    [System.Diagnostics.DebuggerHidden]
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static Snowflake NewId(System.UInt32 value, System.UInt16 machineId, SnowflakeType type) => new(value, machineId, type);

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
    [System.Diagnostics.DebuggerHidden]
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
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
    [System.Diagnostics.DebuggerHidden]
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static Snowflake NewId(SnowflakeType type, System.UInt16 machineId = 1)
    {
        lock (_generatorLock)
        {
            // Use milliseconds for better resolution (1000x better than seconds)
            System.Int64 timestampMs = Clock.UnixMillisecondsNow();

            if (timestampMs == _lastTimestampMs)
            {
                // Same millisecond - increment sequence
                _sequence++;

                // Sequence overflow check
                if (_sequence > MaxSequence)
                {
                    // Exceeded max IDs per millisecond - wait for next ms
                    System.Threading.SpinWait sw = new();
                    do
                    {
                        sw.SpinOnce();
                        timestampMs = Clock.UnixMillisecondsNow();
                    }
                    while (timestampMs == _lastTimestampMs);

                    _lastTimestampMs = timestampMs;
                    _sequence = 0;
                }
            }
            else if (timestampMs > _lastTimestampMs)
            {
                // New millisecond - reset sequence
                _lastTimestampMs = timestampMs;
                _sequence = 0;
            }
            else
            {
                // Clock moved backwards - this is a serious error
                throw new System.InvalidOperationException(
                    $"Clock moved backwards! Last={_lastTimestampMs}ms, Current={timestampMs}ms.  " +
                    "This typically indicates system clock adjustment or NTP sync issues.");
            }

            // Combine timestamp (lower 32 bits) with sequence in upper bits
            // Since timestamp is in ms and grows slowly, we can safely use lower 32 bits
            // and mix sequence into it for uniqueness
            System.UInt32 value = (System.UInt32)(timestampMs & 0xFFFF0000) | _sequence;

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
    /// and is suitable for use in hash-based collections like <see cref="System.Collections.Generic.Dictionary{TKey,TValue}"/>.
    /// This implementation ensures consistent hash values for equal identifiers.
    /// </remarks>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public override System.Int32 GetHashCode() => __combined.GetHashCode();

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
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public override System.Boolean Equals(System.Object? obj) => obj is Snowflake other && Equals(other);

    /// <summary>
    /// Returns the hexadecimal string representation of this identifier.
    /// </summary>
    /// <returns>A 14-character hexadecimal string representing this identifier (7 bytes = 14 hex digits).</returns>
    /// <remarks>
    /// This method efficiently serializes the identifier to a stack-allocated buffer before
    /// converting to hexadecimal format. The returned string is always uppercase and fixed-length.
    /// </remarks>
    [System.Diagnostics.DebuggerHidden]
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public override System.String ToString()
    {
        System.Span<System.Byte> buffer = stackalloc System.Byte[Size];
        _ = TryWriteBytes(buffer);
        return System.Convert.ToHexString(buffer);
    }

    #endregion Override
}
