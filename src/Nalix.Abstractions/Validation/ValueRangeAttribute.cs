// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

namespace Nalix.Abstractions.Validation;

/// <summary>
/// Specifies the numeric inclusive range that a property value must fall within.
/// AOT-safe replacement for <c>System.ComponentModel.DataAnnotations.RangeAttribute</c>.
/// </summary>
/// <remarks>
/// The source generator reads <see cref="Minimum"/> and <see cref="Maximum"/> at compile time
/// and emits static comparison code — no runtime reflection or DataAnnotations dependency.
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class ValueRangeAttribute : Attribute
{
    /// <summary>
    /// Gets the inclusive minimum value.
    /// </summary>
    public double Minimum { get; }

    /// <summary>
    /// Gets the inclusive maximum value.
    /// </summary>
    public double Maximum { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValueRangeAttribute"/> class.
    /// </summary>
    /// <param name="minimum">The inclusive minimum value.</param>
    /// <param name="maximum">The inclusive maximum value.</param>
    public ValueRangeAttribute(double minimum, double maximum)
    {
        this.Minimum = minimum;
        this.Maximum = maximum;
    }
}
