// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.ComponentModel;
using System.Runtime.CompilerServices;

#if DEBUG
[assembly: InternalsVisibleTo("Nalix.Shared.Tests")]
[assembly: InternalsVisibleTo("Nalix.Shared.Benchmarks")]
#endif

namespace Nalix.Shared.Serialization.Internal.Types;

/// <summary>
/// Represents the kind of type.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
internal enum TypeKind : byte
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
