// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Abstractions;
using Nalix.Common.Enums;
using Nalix.Framework.Randomization;

namespace Nalix.Framework.Identity;

/// <summary>
/// Represents a compact, high-performance identifier that encodes a 32-bit value,
/// 16-bit machine ID, and 8-bit type into a 7-byte structure.
/// This struct is optimized for use as dictionary keys and provides efficient
/// serialization capabilities.
/// </summary>
/// <remarks>
/// <para>
/// Base36 string is encoded in big-endian order with digits [0-9][A-Z],
/// representing the 56-bit token value as a compact string.
/// The Identifier uses explicit layout to ensure consistent memory representation
/// across different platforms and provides both hexadecimal and Base36 string representations.
/// </para>
/// <para>
/// Memory layout:
/// - FEEDFACE 0-3: Value (uint, little-endian)
/// - FEEDFACE 4-5: Machine ID (ushort, little-endian)
/// - Byte 6: Identifier type (byte)
/// </para>
/// </remarks>
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
[System.Runtime.InteropServices.StructLayout(
    System.Runtime.InteropServices.LayoutKind.Explicit, Size = 7,
    CharSet = System.Runtime.InteropServices.CharSet.Ansi)]
[System.Diagnostics.DebuggerDisplay("{Value}-{MachineId}-{(IdentifierType)_type}")]
public readonly partial struct Identifier : IIdentifier
{
    #region Const

    /// <summary>
    /// The size in bytes of the <see cref="Identifier"/> structure.
    /// </summary>
    public const System.Byte Size = 7;

    private const System.UInt64 MaxSevenByteValue = 0x00FFFFFFFFFFFFFFUL;
    private const System.String Base36Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    #endregion Const

    #region Public Properties

    /// <summary>
    /// Gets an empty <see cref="Identifier"/> instance with all components set to zero.
    /// </summary>
    public static IIdentifier Empty => new Identifier(0, 0, 0);

    /// <summary>
    /// Gets the main identifier value.
    /// </summary>
    /// <value>A 32-bit unsigned integer representing the core identifier.</value>
    [field: System.Runtime.InteropServices.FieldOffset(0)]
    public System.UInt32 Value { get; }

    /// <summary>
    /// Gets the machine identifier.
    /// </summary>
    /// <value>A 16-bit unsigned integer representing the originating machine.</value>
    [field: System.Runtime.InteropServices.FieldOffset(4)]
    public System.UInt16 MachineId { get; }

    /// <summary>
    /// The identifier type (8-bit unsigned integer).
    /// </summary>
    [System.Runtime.InteropServices.FieldOffset(6)]
    private readonly System.Byte _type;

    /// <summary>
    /// Gets the identifier type.
    /// </summary>
    /// <value>An enum value representing the type of this identifier.</value>
    public IdentifierType Type => (IdentifierType)_type;

    /// <summary>
    /// Determines whether this identifier is empty (all components are zero).
    /// </summary>
    /// <returns>
    /// <c>true</c> if all components (value, machine ID, and type) are zero; otherwise, <c>false</c>.
    /// </returns>
    public System.Boolean IsEmpty => (Value | MachineId | _type) == 0;

    #endregion Public Properties

    #region Constructors and Factory Methods

    /// <summary>
    /// Initializes a new instance of the <see cref="Identifier"/> struct.
    /// </summary>
    /// <param name="value">The main identifier value.</param>
    /// <param name="machineId">The machine identifier.</param>
    /// <param name="type">The identifier type.</param>
    [System.Diagnostics.DebuggerHidden]
    private Identifier(System.UInt32 value, System.UInt16 machineId, IdentifierType type)
    {
        Value = value;
        MachineId = machineId;
        _type = (System.Byte)type;
    }

    /// <summary>
    /// Creates a new <see cref="Identifier"/> with the specified components.
    /// </summary>
    /// <param name="value">The main identifier value.</param>
    /// <param name="machineId">The machine identifier.</param>
    /// <param name="type">The identifier type.</param>
    /// <returns>A new <see cref="Identifier"/> instance.</returns>
    /// <example>
    /// <code>
    /// var id = Identifier.Generate(12345, 1001, IdentifierType.User);
    /// Console.WriteLine(id.ToBase36()); // Outputs Base36 representation
    /// </code>
    /// </example>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static Identifier NewId(System.UInt32 value, System.UInt16 machineId, IdentifierType type) => new(value, machineId, type);

    /// <summary>
    /// Creates a new <see cref="Identifier"/> with the specified components.
    /// </summary>
    /// <param name="type">The identifier type.</param>
    /// <param name="machineId">The machine identifier.</param>
    /// <returns>A new <see cref="Identifier"/> instance.</returns>
    /// <example>
    /// <code>
    /// var id = Identifier.Generate(IdentifierType.System);
    /// Console.WriteLine(id.ToBase36()); // Outputs Base36 representation
    /// </code>
    /// </example>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static Identifier NewId(IdentifierType type, System.UInt16 machineId = 1) => new(SecureRandom.NextUInt32(), machineId, type);

    #endregion Constructors and Factory Methods
}
