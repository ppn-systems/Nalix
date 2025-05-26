namespace Nalix.Serialization.Internal.Reflection;

internal readonly record struct FieldSchema(
    System.Int32 Order,
    System.String Name,
    System.Boolean IsValueType,
    System.Type FieldType,
    System.Reflection.FieldInfo FieldInfo
);
