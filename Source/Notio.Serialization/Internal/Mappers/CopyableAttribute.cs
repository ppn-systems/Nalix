using System;

namespace Notio.Serialization.Internal.Mappers;

/// <summary>
/// Represents an attribute to select which properties are copyable between objects.
/// </summary>
/// <seealso cref="Attribute" />
[AttributeUsage(AttributeTargets.Property)]
internal class CopyableAttribute : Attribute
{
}