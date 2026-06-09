// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

namespace Nalix.Abstractions.Validation;

/// <summary>
/// Specifies the inclusive <see cref="TimeSpan"/> range that a property value must fall within.
/// AOT-safe replacement for <c>[Range(typeof(TimeSpan), ...)]</c>.
/// </summary>
/// <remarks>
/// The source generator reads <see cref="Minimum"/> and <see cref="Maximum"/> at compile time
/// and emits static <see cref="TimeSpan"/> comparison code — no runtime reflection or
/// DataAnnotations dependency.
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class DurationRangeAttribute : Attribute
{
    /// <summary>
    /// Gets the inclusive minimum value as a parseable time string (e.g. "00:00:01").
    /// </summary>
    public string Minimum { get; }

    /// <summary>
    /// Gets the inclusive maximum value as a parseable time string (e.g. "1.00:00:00").
    /// </summary>
    public string Maximum { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DurationRangeAttribute"/> class.
    /// </summary>
    /// <param name="minimum">The inclusive minimum value (e.g. "00:00:01").</param>
    /// <param name="maximum">The inclusive maximum value (e.g. "1.00:00:00").</param>
    public DurationRangeAttribute(string minimum, string maximum)
    {
        this.Minimum = minimum;
        this.Maximum = maximum;
    }
}
