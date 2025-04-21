using Nalix.Shared.Configuration.Attributes;
using Nalix.Shared.Configuration.Metadata;
using Nalix.Shared.Internal;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Nalix.Shared.Configuration.Binding;

public partial class ConfigurationBinder
{
    /// <summary>
    /// Creates configuration metadata for a type.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ConfigurationMetadata CreateConfigurationMetadata(Type type)
    {
        List<PropertyMetadata> bindableProperties = [];

        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            // Skip properties with the ConfiguredIgnore attribute
            if (property.IsDefined(typeof(ConfiguredIgnoreAttribute)))
                continue;

            // Skip properties that can't be written to
            if (!property.CanWrite)
                continue;

            // Create the property metadata
            PropertyMetadata propertyMetadata = new()
            {
                PropertyInfo = property,
                Name = property.Name,
                PropertyType = property.PropertyType,
                TypeCode = Type.GetTypeCode(property.PropertyType)
            };

            bindableProperties.Add(propertyMetadata);
        }

        return new ConfigurationMetadata
        {
            ConfigurationType = type,
            BindableProperties = [.. bindableProperties]
        };
    }

    /// <summary>
    /// Gets the configuration metadata for a type, creating it if it doesn't exist.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ConfigurationMetadata GetOrCreateMetadata(Type type)
        => _metadataCache.GetOrAdd(type, CreateConfigurationMetadata);
}
