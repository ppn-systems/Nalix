// Copyright (c) 2025 PPN Corporation. All rights reserved.

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Serialization.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Serialization.Benchmarks")]
#endif

namespace Nalix.Shared.Serialization.Internal.Reflection;

/// <summary>
/// Represents metadata for a field, including its type and ordering.
/// </summary>
internal readonly record struct FieldSchema(
    // The order of the field in the serialized structure.
    System.Int32 Order,

    // The name of the field.
    System.String Name,

    // Indicates whether the field is a value type.
    System.Boolean IsValueType,

    // The type of the field.
    System.Type FieldType,

    // Reflection metadata for the field.
    System.Reflection.FieldInfo FieldInfo
);