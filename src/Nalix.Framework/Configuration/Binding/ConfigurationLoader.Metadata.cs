// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Reflection;
using System.Runtime.CompilerServices;
using Nalix.Common.Shared;
using Nalix.Framework.Configuration.Internal;

namespace Nalix.Framework.Configuration.Binding;

public partial class ConfigurationLoader
{
    /// <summary>
    /// Creates configuration metadata for a type.
    /// </summary>
    [Pure]
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ConfigurationMetadata CreateConfigurationMetadata(Type type)
    {
        List<PropertyMetadata> bindableProperties = [];
        string? sectionComment = CustomAttributeExtensions.GetCustomAttribute<IniCommentAttribute>(type)?.Comment;

        foreach (PropertyInfo property in type.GetProperties(
                 BindingFlags.Public | BindingFlags.Instance))
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

            string? propComment = CustomAttributeExtensions.GetCustomAttribute<IniCommentAttribute>(property, inherit: true)?.Comment;

            // Create the property metadata
            PropertyMetadata propertyMetadata = new()
            {
                Name = property.Name,
                Comment = propComment,
                PropertyInfo = property,
                PropertyType = property.PropertyType,
                TypeCode = Type.GetTypeCode(property.PropertyType)
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
    [Pure]
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ConfigurationMetadata GetOrCreateMetadata(Type type) => _metadataCache.GetOrAdd(type, CreateConfigurationMetadata);
}
