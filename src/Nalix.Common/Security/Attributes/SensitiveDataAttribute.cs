// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Security.Enums;

namespace Nalix.Common.Security.Attributes;

/// <summary>
/// Indicates that a field or property contains sensitive personal data (PII)
/// that must be handled securely, such as being encrypted at rest or in transit.
/// </summary>
/// <remarks>
/// This attribute is intended for use by security, serialization, logging,
/// or auditing components to automatically apply protection mechanisms.
///
/// <para>
/// Common examples of sensitive data include:
/// </para>
/// <list type="bullet">
/// <item><description>Passwords</description></item>
/// <item><description>Credit card numbers</description></item>
/// <item><description>Social Security Numbers (SSN)</description></item>
/// <item><description>Email addresses</description></item>
/// <item><description>Phone numbers</description></item>
/// <item><description>Physical or mailing addresses</description></item>
/// </list>
/// </remarks>
[System.AttributeUsage(
    System.AttributeTargets.Property | System.AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class SensitiveDataAttribute : System.Attribute
{
    /// <summary>
    /// Gets or sets the sensitivity level of the data.
    /// </summary>
    /// <remarks>
    /// This value can be used for auditing, logging policies,
    /// or selecting different encryption or masking strategies
    /// based on data criticality.
    /// </remarks>
    public DataSensitivityLevel Level { get; set; } = DataSensitivityLevel.High;

    /// <summary>
    /// Initializes a new instance of the <see cref="SensitiveDataAttribute"/> class
    /// with the default sensitivity level.
    /// </summary>
    public SensitiveDataAttribute()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SensitiveDataAttribute"/> class
    /// with the specified sensitivity level.
    /// </summary>
    /// <param name="level">
    /// The sensitivity level that describes how critical the data is.
    /// </param>
    public SensitiveDataAttribute(DataSensitivityLevel level) => Level = level;
}
