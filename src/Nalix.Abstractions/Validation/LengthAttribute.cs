// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

namespace Nalix.Abstractions.Validation;

/// <summary>
/// Specifies the minimum length for a string or collection property.
/// AOT-safe replacement for <c>System.ComponentModel.DataAnnotations.MinLengthAttribute</c>.
/// </summary>
/// <remarks>
/// The source generator reads <see cref="Minimum"/> at compile time and emits a static
/// length check — no runtime reflection or DataAnnotations dependency.
/// Applies to <see cref="string"/>, arrays, and collection types.
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class LengthAttribute : Attribute
{
    /// <summary>
    /// Gets the minimum allowed length.
    /// </summary>
    public int Minimum { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="LengthAttribute"/> class.
    /// </summary>
    /// <param name="minimum">The minimum allowed length.</param>
    public LengthAttribute(int minimum) => this.Minimum = minimum;
}
