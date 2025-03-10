using System;

namespace Notio.Common.Attributes;

/// Indicates that a property should be excluded from JSON serialization and deserialization processes.
/// When applied to a property, this attribute instructs the <see cref="Notio.Serialization.Json"/> serializer
/// to ignore the property entirely, preventing it from appearing in the resulting JSON output or being
/// populated during deserialization.
/// </summary>
/// <remarks>
/// This attribute is useful for excluding sensitive data, computed properties, or fields that are not
/// relevant to the JSON representation of an object. It takes precedence in the serialization logic
/// and works in conjunction with other serialization options provided via <see cref="SerializerOptions"/>.
/// </remarks>
/// <example>
/// <code>
/// public class Person
/// {
///     public string Name { get; set; }
/// 
///     [JsonPropertyIgnore]
///     public int SensitiveData { get; set; }
/// }
/// 
/// var person = new Person { Name = "Geo", SensitiveData = 123 };
/// string json = Json.Serialize(person, new SerializerOptions());
/// // Output: {"Name": "Geo"} (SensitiveData is ignored)
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property)]
public sealed class JsonPropertyIgnoreAttribute : Attribute
{
}
