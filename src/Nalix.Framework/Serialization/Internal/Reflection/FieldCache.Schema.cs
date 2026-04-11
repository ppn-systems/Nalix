// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Reflection;

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Framework.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Framework.Benchmarks")]
#endif

namespace Nalix.Framework.Serialization.Internal.Reflection;

/// <summary>
/// Describes one serialized field and the metadata needed to read or write it
/// without re-running reflection.
/// </summary>
/// <param name="Order">The effective serialization order used by the serializer.</param>
/// <param name="IsHeader">Whether the field must be emitted before regular payload fields.</param>
/// <param name="Size">The field size estimate used for layout sorting and packing heuristics.</param>
/// <param name="Name">The field name as declared on the type.</param>
/// <param name="IsValueType">Whether the field type is a value type and can be copied by value safely.</param>
/// <param name="FieldType">The runtime field type.</param>
/// <param name="FieldInfo">The reflection handle kept for getter/setter emission and fallback access.</param>
[EditorBrowsable(EditorBrowsableState.Never)]
internal readonly record struct FieldSchema(
    int Order,
    bool IsHeader,
    int Size,
    string Name,
    bool IsValueType,
    Type FieldType,
    FieldInfo FieldInfo
);
