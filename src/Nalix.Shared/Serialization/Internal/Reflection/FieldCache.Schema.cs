// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Benchmarks")]
#endif

namespace Nalix.Shared.Serialization.Internal.Reflection;

/// <summary>
/// Represents metadata for a field, including its type and ordering.
/// </summary>
/// <param name="Order"></param>
/// <param name="Name"></param>
/// <param name="IsValueType"></param>
/// <param name="FieldType"></param>
/// <param name="FieldInfo"></param>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
internal readonly record struct FieldSchema(
    // The order of the field in the serialized structure.
    int Order,

    // The name of the field.
    string Name,

    // Indicates whether the field is a value type.
    bool IsValueType,

    // The type of the field.
    System.Type FieldType,

    // Reflection metadata for the field.
    System.Reflection.FieldInfo FieldInfo
);
