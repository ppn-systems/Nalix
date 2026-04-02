// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Reflection;
using System.Runtime.CompilerServices;
using Nalix.Abstractions;
using Nalix.Environment.Configuration.Internal;

namespace Nalix.Environment.Configuration.Binding;

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

            TypeCode typeCode = Type.GetTypeCode(property.PropertyType);

            // SEC-22: Only include supported types to avoid shallow-copy side-effects in Clone<T>
            // and NotSupportedException during initialization.
            if (!IsSupportedType(property.PropertyType, typeCode))
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
                TypeCode = typeCode
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
    private static ConfigurationMetadata GetOrCreateMetadata(Type type) => s_metadataCache.GetOrAdd(type, CreateConfigurationMetadata);

    private static bool IsSupportedType(Type type, TypeCode code)
    {
        if (type.IsEnum)
        {
            return true;
        }

        if (code != TypeCode.Object && code != TypeCode.Empty && code != TypeCode.DBNull)
        {
            return true;
        }

        return type == typeof(Guid) || type == typeof(TimeSpan);
    }

}
