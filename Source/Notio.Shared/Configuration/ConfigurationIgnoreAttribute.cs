using System;

namespace Notio.Shared.Configuration;

/// <summary>
/// An attribute that indicates that a property should be ignored during configuration container initialization.
/// </summary>
/// <remarks>
/// Properties marked with this attribute will not be set when loading values from a configuration file.
/// </remarks>
[AttributeUsage(AttributeTargets.Property)]
public class ConfiguredIgnoreAttribute : Attribute
{ }