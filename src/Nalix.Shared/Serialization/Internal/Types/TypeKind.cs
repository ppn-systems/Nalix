// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Benchmarks")]
#endif

namespace Nalix.Shared.Serialization.Internal.Types;

/// <summary>
/// Represents the kind of type.
/// </summary>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
internal enum TypeKind : System.Byte
{
    /// <summary>
    /// No specific type assigned.
    /// </summary>
    None = 0,

    /// <summary>
    /// Represents an unmanaged single-dimensional array.
    /// </summary>
    UnmanagedSZArray = 1,

    /// <summary>
    /// Represents a fixed-size serializable type.
    /// </summary>
    FixedSizeSerializable = 2,
}
