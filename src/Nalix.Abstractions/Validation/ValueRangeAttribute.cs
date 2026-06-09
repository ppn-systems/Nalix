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
/// Use the <c>(double, double)</c> overload for int/double/float/ushort properties,
/// and the <c>(long, long)</c> overload when the range bound is <c>int.MaxValue</c>,
/// <c>long.MaxValue</c>, or another <see cref="long"/> sentinel.
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class ValueRangeAttribute : Attribute
{
    /// <summary>
    /// Gets the inclusive minimum value (set only for the <see cref="double"/> overload).
    /// </summary>
    public double Minimum { get; }

    /// <summary>
    /// Gets the inclusive maximum value (set only for the <see cref="double"/> overload).
    /// </summary>
    public double Maximum { get; }

    /// <summary>
    /// Gets the inclusive minimum value as a <see cref="long"/> (set only for the <c>(long, long)</c> overload).
    /// </summary>
    public long MinimumInt64 { get; }

    /// <summary>
    /// Gets the inclusive maximum value as a <see cref="long"/> (set only for the <c>(long, long)</c> overload).
    /// </summary>
    public long MaximumInt64 { get; }

    /// <summary>
    /// Gets a value indicating whether this instance was constructed with the <c>(long, long)</c> overload.
    /// When <see langword="true"/>, the source generator emits <see cref="long"/> literal bounds.
    /// </summary>
    public bool UseInt64 { get; }

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

    /// <summary>
    /// Initializes a new instance of the <see cref="ValueRangeAttribute"/> class with
    /// <see cref="long"/> bounds, used when the range includes <c>int.MaxValue</c>,
    /// <c>long.MaxValue</c>, or other values that lose precision as <see cref="double"/>.
    /// </summary>
    /// <param name="minimum">The inclusive minimum value.</param>
    /// <param name="maximum">The inclusive maximum value.</param>
    public ValueRangeAttribute(long minimum, long maximum)
    {
        this.MinimumInt64 = minimum;
        this.MaximumInt64 = maximum;
        this.UseInt64 = true;
    }
}
