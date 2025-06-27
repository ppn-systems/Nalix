using Nalix.Shared.Configuration.Attributes;
using Nalix.Shared.Configuration.Metadata;
using Nalix.Shared.Internal;

namespace Nalix.Shared.Configuration.Binding;

public partial class ConfigurationLoader
{
    /// <summary>
    /// Creates configuration metadata for a type.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static ConfigurationMetadata CreateConfigurationMetadata(System.Type type)
    {
        System.Collections.Generic.List<PropertyMetadata> bindableProperties = [];

        foreach (System.Reflection.PropertyInfo property in type.GetProperties(
                 System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            // Skip properties with the ConfiguredIgnore attribute
            if (property.IsDefined(typeof(ConfiguredIgnoreAttribute), inherit: true))
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
                TypeCode = System.Type.GetTypeCode(property.PropertyType)
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static ConfigurationMetadata GetOrCreateMetadata(System.Type type)
        => _metadataCache.GetOrAdd(type, CreateConfigurationMetadata);
}
