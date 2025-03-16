// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Framework.Identity;

public readonly partial struct Identifier
{
    /// <summary>
    /// Determines whether this identifier is empty (all components are zero).
    /// </summary>
    /// <returns>
    /// <c>true</c> if all components (value, machine ID, and type) are zero; otherwise, <c>false</c>.
    /// </returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean IsEmpty() => (Value | MachineId | _type) == 0;

    /// <summary>
    /// Determines whether this identifier is valid (not empty).
    /// </summary>
    /// <returns>
    /// <c>true</c> if the identifier is not empty; otherwise, <c>false</c>.
    /// </returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean IsValid() => !IsEmpty();
}
