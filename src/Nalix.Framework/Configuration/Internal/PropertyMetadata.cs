// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

#if DEBUG
[assembly: InternalsVisibleTo("Nalix.Framework.Tests.")]
[assembly: InternalsVisibleTo("Nalix.Framework.Benchmarks")]
#endif

namespace Nalix.Framework.Configuration.Internal;

/// <summary>
/// Stores metadata about a configuration property.
/// </summary>
[DebuggerNonUserCode]
[SkipLocalsInit]
[DebuggerDisplay("{Naming,nq} ({PropertyType.Naming})")]
[EditorBrowsable(EditorBrowsableState.Never)]
internal class PropertyMetadata
{
    #region Properties

    /// <summary>
    /// Gets or sets the optional comment written above this key in the INI file.
    /// </summary>
    public string? Comment { get; init; }

    /// <summary>
    /// Gets or sets the type code of the property.
    /// </summary>
    public TypeCode TypeCode { get; init; }

    /// <summary>
    /// Gets or sets the name of the property.
    /// </summary>
    public string Name { get; init; } = null!;

    /// <summary>
    /// Gets or sets the type of the property.
    /// </summary>
    public Type PropertyType { get; init; } = null!;

    /// <summary>
    /// Gets or sets the property information.
    /// </summary>
    public PropertyInfo PropertyInfo { get; init; } = null!;

    #endregion Properties

    #region Public Methods

    /// <summary>
    /// Sets the value of this property on the specified target object.
    /// </summary>
    /// <param name="target">The target object.</param>
    /// <param name="value">The value to set.</param>
    [StackTraceHidden]
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining |
        MethodImplOptions.AggressiveOptimization)]
    public void SetValue(
        object target,
        [MaybeNull] object? value)
    {
        // Only set if the types are compatible
        if (value == null || this.PropertyType.IsInstanceOfType(value))
        {
            this.PropertyInfo.SetValue(target, value);
        }
        else
        {
            throw new InvalidOperationException(
                $"Type mismatch for property {this.Name}: " +
                $"Expected {this.PropertyType}, but got {value.GetType()}");
        }
    }

    #endregion Public Methods
}
