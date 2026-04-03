// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;

#if DEBUG
[assembly: InternalsVisibleTo("Nalix.Framework.Tests")]
[assembly: InternalsVisibleTo("Nalix.Framework.Benchmarks")]
#endif

namespace Nalix.Framework.Serialization.Internal.Reflection;

/// <summary>
/// Represents metadata for a field, including its type and ordering.
/// </summary>
/// <param name="Order">The order of the field in the serialized structure.</param>
/// <param name="IsHeader">Indicates whether the field is a header.</param>
/// <param name="Size">The memory size of the field type.</param>
/// <param name="Name">The name of the field.</param>
/// <param name="IsValueType">Indicates whether the field is a value type.</param>
/// <param name="FieldType">The type of the field.</param>
/// <param name="FieldInfo">Reflection metadata for the field.</param>
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
