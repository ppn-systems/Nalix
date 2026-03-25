// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.CodeAnalysis;

namespace Nalix.Common.Shared;

/// <summary>
/// Specifies the data type associated with a field.
/// </summary>
/// <remarks>
/// This attribute is used to indicate the specific data type for a field, enabling metadata-driven processing or validation.
/// It can only be applied to fields.
/// </remarks>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class DataTypeAttribute : Attribute
{
    /// <summary>
    /// Gets the data type associated with the field.
    /// </summary>
    public Type DataType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DataTypeAttribute"/> class with the specified data type.
    /// </summary>
    /// <param name="dataType">The <see cref="Type"/> to associate with the field.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dataType"/> is null.</exception>
    [SuppressMessage(
        "Style", "IDE0290:Use primary constructor", Justification = "<Pending>")]
    public DataTypeAttribute(Type dataType)
        => DataType = dataType ?? throw new ArgumentNullException(nameof(dataType));
}
