namespace Nalix.Shared.Serialization.Internal.Reflection;

/// <summary>
/// Represents metadata for a field, including its type and ordering.
/// </summary>
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
