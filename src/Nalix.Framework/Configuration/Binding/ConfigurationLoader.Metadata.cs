// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Shared;
using Nalix.Framework.Configuration.Internal;

namespace Nalix.Framework.Configuration.Binding;

public partial class ConfigurationLoader
{
    /// <summary>
    /// Creates configuration metadata for a type.
    /// </summary>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    private static ConfigurationMetadata CreateConfigurationMetadata(System.Type type)
    {
        System.Collections.Generic.List<PropertyMetadata> bindableProperties = [];
        string? sectionComment = System.Reflection.CustomAttributeExtensions.GetCustomAttribute<IniCommentAttribute>(type)?.Comment;

        foreach (System.Reflection.PropertyInfo property in type.GetProperties(
                 System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            // Skip properties with the ConfiguredIgnore attribute
            if (property.IsDefined(typeof(ConfiguredIgnoreAttribute), inherit: true))
            {
                continue;
            }

            // Skip properties that can't be written to
            if (!property.CanWrite)
            {
                continue;
            }

            string? propComment = System.Reflection.CustomAttributeExtensions.GetCustomAttribute<IniCommentAttribute>(property, inherit: true)?.Comment;

            // Create the property metadata
            PropertyMetadata propertyMetadata = new()
            {
                Name = property.Name,
                Comment = propComment,
                PropertyInfo = property,
                PropertyType = property.PropertyType,
                TypeCode = System.Type.GetTypeCode(property.PropertyType)
            };

            bindableProperties.Add(propertyMetadata);
        }

        return new ConfigurationMetadata
        {
            ConfigurationType = type,
            SectionComment = sectionComment,
            BindableProperties = [.. bindableProperties]
        };
    }

    /// <summary>
    /// Gets the configuration metadata for a type, creating it if it doesn't exist.
    /// </summary>
    [System.Diagnostics.Contracts.Pure]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [return: System.Diagnostics.CodeAnalysis.NotNull]
    private static ConfigurationMetadata GetOrCreateMetadata(System.Type type) => _metadataCache.GetOrAdd(type, CreateConfigurationMetadata);
}
